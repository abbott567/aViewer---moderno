import AppKit

/// The main inspection window.
///
/// Structure follows the Windows build: a toolbar of inspection switches and a
/// depth control, a split view with the accessibility tree on the left and the
/// property grid on the right, and a status line that narrates what just
/// happened. There is no API tab strip — macOS publishes one accessibility API,
/// so the tree is always the AX tree.
final class MainWindowController: NSObject, NSWindowDelegate, NSSplitViewDelegate {

    private enum InspectionMode { case none, pointer, focus }

    // Inspection
    private let inspector = AXInspector()
    private let inspectionQueue = DispatchQueue(
        label: "aviewer.inspection", qos: .userInitiated)
    private var inspectionTimer: Timer?
    private var inspectionMode = InspectionMode.none
    private var inspectionBusy = false
    private var inspectionGeneration = 0
    private var lastElementId: String?
    private var lastElementRefresh = Date.distantPast

    // State
    private var activeRoot: AccessibilityNode?
    private var selectedNode: AccessibilityNode?
    private var lastExternalTarget: AccessibilityNode?
    private var focusOrderSteps: [FocusOrderStep] = []
    private var recordingFocusOrder = false
    private var focusCaptureGeneration = 0

    // Services
    private let settings = AppSettingsService()
    private let propertyFilter = PropertyFilterService()
    private let keyMonitor = GlobalFocusKeyMonitor()

    // Overlays
    private let focusRing = FocusRingWindow()
    private let relationshipOverlay = RelationshipOverlayWindow()
    private let focusOrderOverlay = FocusOrderOverlayWindow()

    // UI
    let window: NSWindow
    private let treeController = AccessibilityTreeController()
    private let propertyController = PropertyTableController()
    private let pointerButton = NSButton()
    private let focusButton = NSButton()
    private let focusOrderButton = NSButton()
    private let depthPopUp = NSPopUpButton()
    private let depthLabel = NSTextField(labelWithString: "")
    private let upButton = NSButton()
    private let propertyHeading = NSTextField(labelWithString: "")
    private let treeHeading = NSTextField(labelWithString: "")
    private let statusField = NSTextField(labelWithString: "")
    private var localKeyMonitor: Any?

    private var propertySelection: PropertySelectionWindowController?

    override init() {
        window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 1080, height: 680),
            styleMask: [.titled, .closable, .miniaturizable, .resizable],
            backing: .buffered,
            defer: false)
        super.init()

        Localization.shared.setLanguage(settings.uiLanguage)
        window.title = L("AppTitle")
        window.minSize = NSSize(width: 760, height: 480)
        window.delegate = self
        window.center()
        window.setFrameAutosaveName("AViewerMainWindow")

        buildContent()
        applyStoredSettings()
        installLocalKeyMonitor()

        treeController.onSelect = { [weak self] node in self?.select(node) }
        status(AXPermissions.isTrusted ? L("Ready") : L("PermissionMissingStatus"))
    }

    deinit {
        if let localKeyMonitor { NSEvent.removeMonitor(localKeyMonitor) }
    }

    // MARK: - Layout

    private func buildContent() {
        pointerButton.title = L("PointerInspect")
        pointerButton.setButtonType(.pushOnPushOff)
        pointerButton.bezelStyle = .rounded
        pointerButton.target = self
        pointerButton.action = #selector(togglePointerInspection(_:))

        focusButton.title = L("FocusInspect")
        focusButton.setButtonType(.pushOnPushOff)
        focusButton.bezelStyle = .rounded
        focusButton.target = self
        focusButton.action = #selector(toggleFocusInspection(_:))

        focusOrderButton.title = L("FocusOrder")
        focusOrderButton.bezelStyle = .rounded
        focusOrderButton.target = self
        focusOrderButton.action = #selector(toggleFocusOrderRecording(_:))

        upButton.title = L("MenuUpOneLevel")
        upButton.target = self
        upButton.action = #selector(upOneLevel(_:))
        upButton.bezelStyle = .rounded

        depthLabel.stringValue = L("TreeDepth")
        depthPopUp.addItems(withTitles: (0...4).map(String.init))
        depthPopUp.setAccessibilityLabel(L("TreeDepth"))
        depthPopUp.target = self
        depthPopUp.action = #selector(depthChanged(_:))

        let spacer = NSView()
        spacer.setContentHuggingPriority(.defaultLow, for: .horizontal)

        let toolbar = NSStackView(views: [
            pointerButton, focusButton, focusOrderButton, upButton,
            spacer, depthLabel, depthPopUp
        ])
        toolbar.orientation = .horizontal
        toolbar.spacing = 8
        toolbar.edgeInsets = NSEdgeInsets(top: 8, left: 12, bottom: 8, right: 12)

        treeHeading.stringValue = L("AccessibilityTree")
        treeHeading.font = .boldSystemFont(ofSize: NSFont.systemFontSize)
        let treePane = NSStackView(views: [treeHeading, treeController.scrollView])
        treePane.orientation = .vertical
        treePane.spacing = 6
        treePane.alignment = .leading
        treeController.scrollView.translatesAutoresizingMaskIntoConstraints = false
        treeController.scrollView.widthAnchor.constraint(
            equalTo: treePane.widthAnchor).isActive = true

        propertyHeading.stringValue = L("Properties")
        propertyHeading.font = .boldSystemFont(ofSize: NSFont.systemFontSize)
        let propertyPane = NSStackView(views: [propertyHeading, propertyController.scrollView])
        propertyPane.orientation = .vertical
        propertyPane.spacing = 6
        propertyPane.alignment = .leading
        propertyController.scrollView.translatesAutoresizingMaskIntoConstraints = false
        propertyController.scrollView.widthAnchor.constraint(
            equalTo: propertyPane.widthAnchor).isActive = true

        let split = NSSplitView()
        split.isVertical = true
        split.dividerStyle = .thin
        split.delegate = self
        split.addArrangedSubview(treePane)
        split.addArrangedSubview(propertyPane)

        statusField.lineBreakMode = .byTruncatingTail
        statusField.setAccessibilityLabel(L("Status"))
        // The status line is the app's running commentary; announcing changes
        // means a screen reader user is not left guessing what a capture found.
        statusField.setAccessibilityRole(.staticText)

        let statusBar = NSStackView(views: [statusField])
        statusBar.orientation = .horizontal
        statusBar.edgeInsets = NSEdgeInsets(top: 6, left: 12, bottom: 6, right: 12)
        statusBar.alignment = .centerY

        let root = NSStackView(views: [toolbar, split, statusBar])
        root.orientation = .vertical
        root.spacing = 0
        root.translatesAutoresizingMaskIntoConstraints = false
        split.translatesAutoresizingMaskIntoConstraints = false
        toolbar.translatesAutoresizingMaskIntoConstraints = false
        statusBar.translatesAutoresizingMaskIntoConstraints = false

        let content = NSView()
        content.addSubview(root)
        NSLayoutConstraint.activate([
            root.leadingAnchor.constraint(equalTo: content.leadingAnchor),
            root.trailingAnchor.constraint(equalTo: content.trailingAnchor),
            root.topAnchor.constraint(equalTo: content.topAnchor),
            root.bottomAnchor.constraint(equalTo: content.bottomAnchor),
            toolbar.widthAnchor.constraint(equalTo: root.widthAnchor),
            split.widthAnchor.constraint(equalTo: root.widthAnchor),
            statusBar.widthAnchor.constraint(equalTo: root.widthAnchor)
        ])
        window.contentView = content
    }

    func splitView(
        _ splitView: NSSplitView,
        constrainMinCoordinate proposedMinimumPosition: CGFloat,
        ofSubviewAt dividerIndex: Int
    ) -> CGFloat { 250 }

    func splitView(
        _ splitView: NSSplitView,
        constrainMaxCoordinate proposedMaximumPosition: CGFloat,
        ofSubviewAt dividerIndex: Int
    ) -> CGFloat { splitView.bounds.width - 330 }

    private func applyStoredSettings() {
        window.level = settings.alwaysOnTop ? .floating : .normal
        inspector.enhancedUserInterface = settings.enhancedUserInterface
        depthPopUp.selectItem(at: min(max(settings.treeDepth, 0), 4))
    }

    // MARK: - Keyboard

    /// F7, F8 and F9 keep working alongside the Command shortcuts, matching the
    /// muscle memory of the Windows build.
    private func installLocalKeyMonitor() {
        localKeyMonitor = NSEvent.addLocalMonitorForEvents(matching: [.keyDown]) {
            [weak self] event in
            guard let self, self.window.isKeyWindow else { return event }
            switch event.keyCode {
            case 98: self.togglePointerInspection(nil); return nil   // F7
            case 100: self.toggleFocusInspection(nil); return nil    // F8
            case 101: self.toggleFocusOrderRecording(nil); return nil // F9
            case 53: return self.handleEscape() ? nil : event        // Escape
            default: return event
            }
        }
    }

    private func handleEscape() -> Bool {
        if recordingFocusOrder {
            setFocusOrderRecording(false)
            return true
        }
        if inspectionMode != .none {
            setInspectionMode(.none)
            return true
        }
        return false
    }

    // MARK: - Inspection control

    private var depth: Int { depthPopUp.indexOfSelectedItem }

    @objc private func depthChanged(_ sender: Any?) {
        settings.treeDepth = depth
    }

    @objc func togglePointerInspection(_ sender: Any?) {
        setInspectionMode(inspectionMode == .pointer ? .none : .pointer)
    }

    @objc func toggleFocusInspection(_ sender: Any?) {
        setInspectionMode(inspectionMode == .focus ? .none : .focus)
    }

    private func setInspectionMode(_ mode: InspectionMode) {
        guard mode == .none || requireAccessibilityAccess() else { return }

        inspectionGeneration += 1
        inspectionMode = mode
        lastElementId = nil
        lastElementRefresh = .distantPast

        pointerButton.state = mode == .pointer ? .on : .off
        focusButton.state = mode == .focus ? .on : .off
        pointerButton.setAccessibilityLabel(
            LF("PointerSwitchState", mode == .pointer ? L("On") : L("Off")))
        focusButton.setAccessibilityLabel(
            LF("FocusSwitchState", mode == .focus ? L("On") : L("Off")))

        inspectionTimer?.invalidate()
        inspectionTimer = nil

        guard mode != .none else {
            focusRing.hideOverlay()
            relationshipOverlay.hideOverlay()
            status(activeRoot == nil ? L("Ready") : L("InspectionStopped"))
            return
        }

        let timer = Timer(timeInterval: 0.2, repeats: true) { [weak self] _ in
            self?.inspectionTick()
        }
        RunLoop.main.add(timer, forMode: .common)
        inspectionTimer = timer
        status(mode == .pointer ? L("PointerInspectionActive") : L("FocusInspectionActive"))
    }

    private func inspectionTick() {
        guard inspectionMode != .none, !inspectionBusy else { return }

        let generation = inspectionGeneration
        let mode = inspectionMode
        let currentDepth = depth
        let point = ScreenGeometry.cursorLocationAX
        inspectionBusy = true

        inspectionQueue.async { [weak self] in
            guard let self else { return }
            let result = mode == .pointer
                ? self.inspector.inspectPoint(point, maxDepth: currentDepth)
                : self.inspector.inspectFocused(maxDepth: currentDepth)

            DispatchQueue.main.async {
                defer { self.inspectionBusy = false }
                guard generation == self.inspectionGeneration,
                      mode == self.inspectionMode else { return }

                guard let root = result.root, root.processId != getpid() else { return }

                // Re-rendering the same element every tick makes the tree
                // unusable, so an unchanged target is only refreshed
                // periodically.
                let now = Date()
                if root.id == self.lastElementId,
                   now.timeIntervalSince(self.lastElementRefresh) < 0.75 { return }

                self.lastElementId = root.id
                self.lastElementRefresh = now
                self.present(
                    result,
                    source: mode == .pointer
                        ? LF("PointerAt", Int(point.x), Int(point.y))
                        : L("KeyboardFocusSource"))
            }
        }
    }

    // MARK: - Presentation

    private func present(_ result: AXInspector.Result, source: String) {
        guard let root = result.root else {
            activeRoot = nil
            selectedNode = nil
            treeController.show(nil)
            propertyController.show([])
            focusRing.hideOverlay()
            relationshipOverlay.hideOverlay()
            status(LF("NoElementFound", source))
            return
        }

        activeRoot = root
        if root.processId != getpid() { lastExternalTarget = root }

        treeHeading.stringValue = LF("ApiAccessibilityTree", "AX")
        treeController.show(root)
        select(root)

        if result.truncated {
            status(LF("TreeTruncated", AXInspector.maxNodes))
        } else {
            status(LF(
                "ElementSummary", source, root.displayRole,
                root.name.isEmpty ? "—" : root.name))
        }
    }

    private func select(_ node: AccessibilityNode) {
        selectedNode = node
        propertyController.show(propertyFilter.filter(node.properties))

        if inspectionMode != .none || activeRoot != nil {
            focusRing.show(around: node.frame)
        }
        refreshRelationshipOverlay()
    }

    private func refreshRelationshipOverlay() {
        guard settings.showRelationships, let node = selectedNode else {
            relationshipOverlay.hideOverlay()
            return
        }
        relationshipOverlay.show(relationshipsOf: node)
    }

    private func status(_ message: String) {
        statusField.stringValue = message
        statusField.setAccessibilityValue(message)
        NSAccessibility.post(element: statusField, notification: .valueChanged)
    }

    // MARK: - Navigation

    @objc func upOneLevel(_ sender: Any?) {
        guard let node = selectedNode, !inspectionBusy else {
            status(L("SelectTreeElementFirst"))
            return
        }
        guard requireAccessibilityAccess() else { return }

        inspectionBusy = true
        let currentDepth = depth
        inspectionQueue.async { [weak self] in
            guard let self else { return }
            let result = self.inspector.inspectParent(of: node, maxDepth: currentDepth)
            DispatchQueue.main.async {
                self.inspectionBusy = false
                guard result.root != nil else {
                    self.status(L("NoParentAvailable"))
                    return
                }
                self.present(result, source: L("ParentSource"))
            }
        }
    }

    @objc func loadCompleteTree(_ sender: Any?) {
        let startingNode: AccessibilityNode?
        if let selected = selectedNode, selected.processId != 0,
           selected.processId != getpid() {
            startingNode = selected
        } else {
            startingNode = lastExternalTarget
        }

        guard let node = startingNode, node.processId != getpid() else {
            status(L("InspectExternalElementFirst"))
            return
        }
        guard requireAccessibilityAccess() else { return }

        // Stop live inspection without discarding the last external target.
        inspectionGeneration += 1
        inspectionMode = .none
        inspectionTimer?.invalidate()
        inspectionTimer = nil
        pointerButton.state = .off
        focusButton.state = .off

        status(L("LoadingCompleteTree"))
        inspectionQueue.async { [weak self] in
            guard let self else { return }
            let result = self.inspector.inspectApplicationRoot(from: node, maxDepth: 64)
            DispatchQueue.main.async {
                guard let root = result.root else {
                    self.status(L("CompleteTreeUnavailable"))
                    return
                }
                self.present(result, source: L("CompleteTreeSource"))
                let name = root.processName.isEmpty ? "\(root.processId)" : root.processName
                self.status(LF(
                    result.truncated ? "CompleteTreeTruncated" : "CompleteTreeLoaded",
                    name, root.nodeCount))
            }
        }
    }

    // MARK: - Focus order recording

    @objc func toggleFocusOrderRecording(_ sender: Any?) {
        setFocusOrderRecording(!recordingFocusOrder)
    }

    private func setFocusOrderRecording(_ enabled: Bool) {
        guard !enabled || requireAccessibilityAccess() else { return }

        recordingFocusOrder = enabled
        focusCaptureGeneration += 1
        focusOrderButton.title = enabled ? L("StopFocusOrder") : L("FocusOrder")

        guard enabled else {
            keyMonitor.stop()
            status(focusOrderSteps.isEmpty
                ? L("RecordingStoppedEmpty")
                : LF("RecordingStopped", focusOrderSteps.count))
            return
        }

        keyMonitor.onNavigationKey = { [weak self] key in
            self?.captureFocusStop(for: key)
        }
        guard keyMonitor.start() else {
            recordingFocusOrder = false
            focusOrderButton.title = L("FocusOrder")
            status(L("PermissionMissingStatus"))
            return
        }
        status(settings.includeArrowNavigation
            ? L("RecordingWithArrows")
            : L("RecordingTabOnly"))
    }

    @objc func clearFocusPath(_ sender: Any?) {
        focusOrderSteps.removeAll()
        focusOrderOverlay.hideOverlay()
        status(L("FocusPathCleared"))
    }

    private func captureFocusStop(for key: FocusNavigationKey) {
        guard recordingFocusOrder,
              !(key.isArrow && !settings.includeArrowNavigation) else { return }

        focusCaptureGeneration += 1
        let generation = focusCaptureGeneration

        // Focus moves after the key is released; give the application a moment
        // to settle before reading it.
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.12) { [weak self] in
            guard let self, self.recordingFocusOrder,
                  generation == self.focusCaptureGeneration else { return }

            self.inspectionQueue.async {
                let result = self.inspector.inspectFocused(maxDepth: 0)
                DispatchQueue.main.async {
                    guard self.recordingFocusOrder,
                          generation == self.focusCaptureGeneration,
                          let focused = result.root,
                          focused.processId != getpid() else { return }

                    if let previous = self.focusOrderSteps.last,
                       previous.element.id == focused.id { return }

                    self.focusOrderSteps.append(FocusOrderStep(
                        sequence: self.focusOrderSteps.count + 1,
                        navigationKey: key,
                        element: focused))
                    self.focusOrderOverlay.show(path: self.focusOrderSteps)
                    self.status(LF(
                        "FocusStop", self.focusOrderSteps.count,
                        focused.displayRole, focused.name.isEmpty ? "—" : focused.name))
                }
            }
        }
    }

    // MARK: - View options

    @objc func toggleRelationships(_ sender: NSMenuItem) {
        settings.showRelationships.toggle()
        sender.state = settings.showRelationships ? .on : .off
        refreshRelationshipOverlay()
        status(settings.showRelationships
            ? L("RelationshipsEnabled") : L("RelationshipsDisabled"))
    }

    @objc func toggleAlwaysOnTop(_ sender: NSMenuItem) {
        settings.alwaysOnTop.toggle()
        sender.state = settings.alwaysOnTop ? .on : .off
        window.level = settings.alwaysOnTop ? .floating : .normal
        status(settings.alwaysOnTop ? L("AlwaysOnTopEnabled") : L("AlwaysOnTopDisabled"))
    }

    @objc func toggleArrowNavigation(_ sender: NSMenuItem) {
        settings.includeArrowNavigation.toggle()
        sender.state = settings.includeArrowNavigation ? .on : .off
        status(settings.includeArrowNavigation
            ? L("ArrowNavigationEnabled") : L("ArrowNavigationDisabled"))
    }

    @objc func toggleEnhancedUserInterface(_ sender: NSMenuItem) {
        settings.enhancedUserInterface.toggle()
        sender.state = settings.enhancedUserInterface ? .on : .off
        inspector.enhancedUserInterface = settings.enhancedUserInterface
        status(settings.enhancedUserInterface
            ? L("EnhancedUserInterfaceOn") : L("EnhancedUserInterfaceOff"))
    }

    @objc func chooseProperties(_ sender: Any?) {
        let available = activeRoot?.flattened().flatMap(\.properties) ?? []
        let controller = PropertySelectionWindowController(
            choices: propertyFilter.choices(from: available))
        propertySelection = controller
        controller.present(in: window) { [weak self] choices in
            guard let self else { return }
            self.propertyFilter.apply(choices)
            if let node = self.selectedNode {
                self.propertyController.show(self.propertyFilter.filter(node.properties))
            }
            self.status(LF("DisplayingProperties", self.propertyController.displayedCount))
            self.propertySelection = nil
        }
    }

    @objc func changeLanguage(_ sender: NSMenuItem) {
        let code = sender.representedObject as? String
        settings.uiLanguage = code
        Localization.shared.setLanguage(code)
        MainMenuBuilder.rebuild(for: self)
        applyLocalisedText()
        status(L("LanguageChanged"))
    }

    /// Re-reads every string in the window so a language change takes effect
    /// immediately, as it does in the Windows build.
    private func applyLocalisedText() {
        window.title = L("AppTitle")
        pointerButton.title = L("PointerInspect")
        focusButton.title = L("FocusInspect")
        focusOrderButton.title = recordingFocusOrder ? L("StopFocusOrder") : L("FocusOrder")
        upButton.title = L("MenuUpOneLevel")
        depthLabel.stringValue = L("TreeDepth")
        depthPopUp.setAccessibilityLabel(L("TreeDepth"))
        propertyHeading.stringValue = L("Properties")
        treeHeading.stringValue = activeRoot == nil
            ? L("AccessibilityTree")
            : LF("ApiAccessibilityTree", "AX")
        statusField.setAccessibilityLabel(L("Status"))
        treeController.applyLocalisedText()
        propertyController.applyLocalisedText()

        pointerButton.setAccessibilityLabel(
            LF("PointerSwitchState", inspectionMode == .pointer ? L("On") : L("Off")))
        focusButton.setAccessibilityLabel(
            LF("FocusSwitchState", inspectionMode == .focus ? L("On") : L("Off")))
    }

    // MARK: - Export

    @objc func copyJSON(_ sender: Any?) {
        guard let root = activeRoot else {
            status(L("SelectTreeElementFirst"))
            return
        }
        writeToPasteboard(JSONExportService.serialize(root))
        status(L("JsonCopied"))
    }

    @objc func copyHTML(_ sender: Any?) {
        guard let node = selectedNode else {
            status(L("SelectTreeElementFirst"))
            return
        }
        writeToPasteboard(HTMLExportService.serializeElement(node))
        status(L("HtmlCopied"))
    }

    @objc func copyHTMLSubtree(_ sender: Any?) {
        guard let node = selectedNode else {
            status(L("SelectTreeElementFirst"))
            return
        }
        writeToPasteboard(HTMLExportService.serializeSubtree(node))
        status(L("HtmlSubtreeCopied"))
    }

    @objc func saveJSON(_ sender: Any?) {
        guard let root = activeRoot else {
            status(L("SelectTreeElementFirst"))
            return
        }

        let panel = NSSavePanel()
        panel.allowedContentTypes = [.json]
        panel.nameFieldStringValue = "ax-accessibility-tree.json"
        panel.beginSheetModal(for: window) { [weak self] response in
            guard let self, response == .OK, let url = panel.url else { return }
            do {
                try JSONExportService.serialize(root).write(
                    to: url, atomically: true, encoding: .utf8)
                self.status(LF("SavedFile", url.lastPathComponent))
            } catch {
                self.status(LF("SaveFailed", error.localizedDescription))
            }
        }
    }

    private func writeToPasteboard(_ text: String) {
        let pasteboard = NSPasteboard.general
        pasteboard.clearContents()
        pasteboard.setString(text, forType: .string)
    }

    // MARK: - Help

    @objc func openHelpLink(_ sender: NSMenuItem) {
        guard let value = sender.representedObject as? String,
              HelpMenuLinkService.isAllowedURL(value),
              let url = URL(string: value) else {
            presentAlert(L("MenuHelp"), L("HelpInvalidLink"))
            return
        }
        NSWorkspace.shared.open(url)
    }

    // MARK: - Permission

    /// Inspection is meaningless without accessibility access, so every entry
    /// point checks and explains rather than silently returning nothing.
    @discardableResult
    private func requireAccessibilityAccess() -> Bool {
        if AXPermissions.isTrusted { return true }

        AXPermissions.requestTrust()
        let alert = NSAlert()
        alert.messageText = L("PermissionTitle")
        alert.informativeText = L("PermissionMessage")
        alert.addButton(withTitle: L("PermissionOpenSettings"))
        alert.addButton(withTitle: L("PermissionRecheck"))
        alert.beginSheetModal(for: window) { response in
            if response == .alertFirstButtonReturn {
                AXPermissions.openAccessibilitySettings()
            } else {
                self.status(AXPermissions.isTrusted
                    ? L("PermissionGrantedStatus")
                    : L("PermissionMissingStatus"))
            }
        }
        status(L("PermissionMissingStatus"))
        return false
    }

    private func presentAlert(_ title: String, _ message: String) {
        let alert = NSAlert()
        alert.messageText = title
        alert.informativeText = message
        alert.beginSheetModal(for: window)
    }

    // MARK: - Window lifecycle

    func show() {
        window.makeKeyAndOrderFront(nil)
    }

    func windowWillClose(_ notification: Notification) {
        inspectionGeneration += 1
        inspectionTimer?.invalidate()
        keyMonitor.stop()
        focusRing.hideOverlay()
        relationshipOverlay.hideOverlay()
        focusOrderOverlay.hideOverlay()
        NSApp.terminate(nil)
    }

    // MARK: - Menu state

    var isRelationshipsEnabled: Bool { settings.showRelationships }
    var isAlwaysOnTop: Bool { settings.alwaysOnTop }
    var isArrowNavigationIncluded: Bool { settings.includeArrowNavigation }
    var isEnhancedUserInterfaceEnabled: Bool { settings.enhancedUserInterface }
    var currentLanguage: String? { settings.uiLanguage }
}
