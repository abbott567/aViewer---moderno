import AppKit

/// The "Choose properties" sheet.
///
/// macOS elements publish far more attributes than the Windows fixed list, so
/// the search field is doing real work here: the catalogue can run to several
/// hundred rows once a browser has been inspected.
final class PropertySelectionWindowController: NSObject, NSTableViewDataSource, NSTableViewDelegate {

    private enum Column {
        static let show = NSUserInterfaceItemIdentifier("show")
        static let section = NSUserInterfaceItemIdentifier("section")
        static let property = NSUserInterfaceItemIdentifier("property")
    }

    private let window: NSWindow
    private let tableView = NSTableView()
    private let searchField = NSSearchField()

    private var choices: [PropertyChoice]
    private var visibleIndexes: [Int] = []
    private var completion: (([PropertyChoice]) -> Void)?

    init(choices: [PropertyChoice]) {
        self.choices = choices
        window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 620, height: 620),
            styleMask: [.titled, .resizable, .closable],
            backing: .buffered,
            defer: false)
        super.init()

        window.title = L("ChoosePropertiesTitle")
        window.minSize = NSSize(width: 480, height: 420)
        buildContent()
        applyFilter("")
    }

    func present(in parent: NSWindow, completion: @escaping ([PropertyChoice]) -> Void) {
        self.completion = completion
        parent.beginSheet(window)
    }

    // MARK: - Layout

    private func buildContent() {
        let description = NSTextField(wrappingLabelWithString: L("ChoosePropertiesDescription"))

        let selectAll = NSButton(
            title: L("SelectAll"), target: self, action: #selector(selectAll(_:)))
        let selectNone = NSButton(
            title: L("SelectNone"), target: self, action: #selector(selectNone(_:)))

        searchField.placeholderString = L("FilterProperties")
        searchField.target = self
        searchField.action = #selector(searchChanged(_:))
        searchField.setAccessibilityLabel(L("FilterProperties"))
        searchField.setContentHuggingPriority(.defaultLow, for: .horizontal)

        let controls = NSStackView(views: [selectAll, selectNone, searchField])
        controls.orientation = .horizontal
        controls.spacing = 8

        configureTable()
        let scrollView = NSScrollView()
        scrollView.documentView = tableView
        scrollView.hasVerticalScroller = true
        scrollView.borderType = .bezelBorder

        let apply = NSButton(title: L("Apply"), target: self, action: #selector(apply(_:)))
        apply.keyEquivalent = "\r"
        let cancel = NSButton(title: L("Cancel"), target: self, action: #selector(cancel(_:)))
        cancel.keyEquivalent = "\u{1b}"

        let spacer = NSView()
        spacer.setContentHuggingPriority(.defaultLow, for: .horizontal)
        let buttons = NSStackView(views: [spacer, cancel, apply])
        buttons.orientation = .horizontal
        buttons.spacing = 10

        let stack = NSStackView(views: [description, controls, scrollView, buttons])
        stack.orientation = .vertical
        stack.spacing = 12
        stack.edgeInsets = NSEdgeInsets(top: 16, left: 16, bottom: 16, right: 16)
        stack.translatesAutoresizingMaskIntoConstraints = false
        stack.setHuggingPriority(.defaultLow, for: .vertical)

        let content = NSView()
        content.addSubview(stack)
        NSLayoutConstraint.activate([
            stack.leadingAnchor.constraint(equalTo: content.leadingAnchor),
            stack.trailingAnchor.constraint(equalTo: content.trailingAnchor),
            stack.topAnchor.constraint(equalTo: content.topAnchor),
            stack.bottomAnchor.constraint(equalTo: content.bottomAnchor)
        ])
        window.contentView = content
    }

    private func configureTable() {
        let show = NSTableColumn(identifier: Column.show)
        show.title = L("Show")
        show.width = 60

        let section = NSTableColumn(identifier: Column.section)
        section.title = L("Section")
        section.width = 130

        let property = NSTableColumn(identifier: Column.property)
        property.title = L("Property")
        property.width = 340
        property.resizingMask = .autoresizingMask

        for column in [show, section, property] { tableView.addTableColumn(column) }
        tableView.dataSource = self
        tableView.delegate = self
        tableView.usesAlternatingRowBackgroundColors = true
        tableView.columnAutoresizingStyle = .lastColumnOnlyAutoresizingStyle
    }

    // MARK: - Actions

    @objc private func searchChanged(_ sender: NSSearchField) {
        applyFilter(sender.stringValue)
    }

    private func applyFilter(_ term: String) {
        let query = term.trimmingCharacters(in: .whitespaces).lowercased()
        visibleIndexes = choices.indices.filter { index in
            guard !query.isEmpty else { return true }
            let choice = choices[index]
            return choice.name.lowercased().contains(query)
                || choice.group.lowercased().contains(query)
        }
        tableView.reloadData()
    }

    /// Select all and clear all act on the filtered rows, so a search narrows
    /// what the bulk buttons affect rather than silently changing everything.
    @objc private func selectAll(_ sender: Any?) {
        for index in visibleIndexes { choices[index].isSelected = true }
        tableView.reloadData()
    }

    @objc private func selectNone(_ sender: Any?) {
        for index in visibleIndexes { choices[index].isSelected = false }
        tableView.reloadData()
    }

    @objc private func toggle(_ sender: NSButton) {
        let row = sender.tag
        guard row < visibleIndexes.count else { return }
        choices[visibleIndexes[row]].isSelected = sender.state == .on
    }

    @objc private func apply(_ sender: Any?) {
        let result = choices
        finish()
        completion?(result)
    }

    @objc private func cancel(_ sender: Any?) {
        finish()
    }

    private func finish() {
        window.sheetParent?.endSheet(window)
        window.orderOut(nil)
    }

    // MARK: - Table

    func numberOfRows(in tableView: NSTableView) -> Int { visibleIndexes.count }

    func tableView(
        _ tableView: NSTableView,
        viewFor tableColumn: NSTableColumn?,
        row: Int
    ) -> NSView? {
        guard let tableColumn, row < visibleIndexes.count else { return nil }
        let choice = choices[visibleIndexes[row]]

        if tableColumn.identifier == Column.show {
            let button = NSButton(checkboxWithTitle: "", target: self, action: #selector(toggle(_:)))
            button.state = choice.isSelected ? .on : .off
            button.tag = row
            button.setAccessibilityLabel(LF("ShowProperty", "\(choice.group), \(choice.name)"))
            return button
        }

        let text = tableColumn.identifier == Column.section ? choice.group : choice.name
        let field = NSTextField(labelWithString: text)
        field.lineBreakMode = .byTruncatingTail
        return field
    }
}
