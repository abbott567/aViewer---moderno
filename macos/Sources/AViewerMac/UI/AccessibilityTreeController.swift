import AppKit

/// Drives the accessibility tree outline view.
///
/// `AccessibilityNode` is a reference type, so it can be handed to
/// `NSOutlineView` as the item directly and identity comparisons stay valid
/// across reloads of the same capture.
final class AccessibilityTreeController: NSObject, NSOutlineViewDataSource, NSOutlineViewDelegate {

    let outlineView = NSOutlineView()
    let scrollView = NSScrollView()

    var onSelect: ((AccessibilityNode) -> Void)?

    private var root: AccessibilityNode?

    override init() {
        super.init()

        let column = NSTableColumn(identifier: NSUserInterfaceItemIdentifier("element"))
        column.title = L("AccessibilityTree")
        column.resizingMask = .autoresizingMask
        outlineView.addTableColumn(column)
        outlineView.outlineTableColumn = column
        outlineView.headerView = nil
        outlineView.dataSource = self
        outlineView.delegate = self
        outlineView.rowSizeStyle = .default
        outlineView.usesAlternatingRowBackgroundColors = true
        outlineView.autoresizesOutlineColumn = false
        outlineView.setAccessibilityLabel(L("AccessibilityTree"))

        scrollView.documentView = outlineView
        scrollView.hasVerticalScroller = true
        scrollView.hasHorizontalScroller = true
        scrollView.autohidesScrollers = true
        scrollView.borderType = .bezelBorder
    }

    /// Re-reads every visible string after a language change.
    func applyLocalisedText() {
        outlineView.tableColumns.first?.title = L("AccessibilityTree")
        outlineView.setAccessibilityLabel(L("AccessibilityTree"))
    }

    /// Replaces the displayed tree and selects the root, matching the Windows
    /// build's behaviour of showing the captured element's properties
    /// immediately.
    func show(_ node: AccessibilityNode?) {
        root = node
        outlineView.reloadData()
        guard let node else { return }
        outlineView.expandItem(node)
        for child in node.children {
            outlineView.expandItem(child)
        }
        let row = outlineView.row(forItem: node)
        if row >= 0 {
            outlineView.selectRowIndexes(IndexSet(integer: row), byExtendingSelection: false)
        }
    }

    // MARK: - Data source

    func outlineView(_ outlineView: NSOutlineView, numberOfChildrenOfItem item: Any?) -> Int {
        guard let item else { return root == nil ? 0 : 1 }
        return (item as? AccessibilityNode)?.children.count ?? 0
    }

    func outlineView(_ outlineView: NSOutlineView, child index: Int, ofItem item: Any?) -> Any {
        guard let node = item as? AccessibilityNode else { return root as Any }
        return node.children[index]
    }

    func outlineView(_ outlineView: NSOutlineView, isItemExpandable item: Any) -> Bool {
        !((item as? AccessibilityNode)?.children.isEmpty ?? true)
    }

    // MARK: - Delegate

    func outlineView(
        _ outlineView: NSOutlineView,
        viewFor tableColumn: NSTableColumn?,
        item: Any
    ) -> NSView? {
        guard let node = item as? AccessibilityNode else { return nil }
        let identifier = NSUserInterfaceItemIdentifier("elementCell")

        let field: NSTextField
        if let reused = outlineView.makeView(withIdentifier: identifier, owner: self)
            as? NSTextField {
            field = reused
        } else {
            field = NSTextField(labelWithString: "")
            field.identifier = identifier
            field.lineBreakMode = .byTruncatingTail
            field.cell?.usesSingleLineMode = true
        }

        field.attributedStringValue = label(for: node)
        // The visual label is a role and a name run together; spell out the
        // relationship for anyone reading this tool with a screen reader.
        field.setAccessibilityLabel(
            node.name.isEmpty
                ? node.displayRole
                : "\(node.displayRole), \(node.name)")
        return field
    }

    func outlineViewSelectionDidChange(_ notification: Notification) {
        let row = outlineView.selectedRow
        guard row >= 0, let node = outlineView.item(atRow: row) as? AccessibilityNode else { return }
        onSelect?(node)
    }

    /// Role in bold, then the accessible name — the same emphasis the Windows
    /// tree template uses.
    private func label(for node: AccessibilityNode) -> NSAttributedString {
        let font = NSFont.systemFont(ofSize: NSFont.systemFontSize)
        let bold = NSFont.boldSystemFont(ofSize: NSFont.systemFontSize)
        let result = NSMutableAttributedString(
            string: node.displayRole,
            attributes: [.font: bold, .foregroundColor: NSColor.labelColor])

        if !node.name.isEmpty {
            result.append(NSAttributedString(
                string: " — \(node.name)",
                attributes: [.font: font, .foregroundColor: NSColor.secondaryLabelColor]))
        }
        return result
    }
}
