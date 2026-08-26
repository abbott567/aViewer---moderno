import AppKit

/// Drives the property grid.
///
/// Values are wrapped rather than truncated: AX values such as a DOM class list
/// or a relationship summary are routinely longer than a column, and the whole
/// point of the grid is to read them.
final class PropertyTableController: NSObject, NSTableViewDataSource, NSTableViewDelegate {

    private enum Column {
        static let section = NSUserInterfaceItemIdentifier("section")
        static let property = NSUserInterfaceItemIdentifier("property")
        static let value = NSUserInterfaceItemIdentifier("value")
    }

    let tableView = NSTableView()
    let scrollView = NSScrollView()

    private var properties: [AccessibilityProperty] = []
    private let showsSectionColumn: Bool

    /// - Parameter showsSectionColumn: omit the section column when every row
    ///   in the table belongs to the same section, where it would only repeat.
    init(showsSectionColumn: Bool = true) {
        self.showsSectionColumn = showsSectionColumn
        super.init()

        let section = NSTableColumn(identifier: Column.section)
        section.title = L("Section")
        section.width = 110
        section.minWidth = 70

        let property = NSTableColumn(identifier: Column.property)
        property.title = L("Property")
        property.width = 165
        property.minWidth = 100

        let value = NSTableColumn(identifier: Column.value)
        value.title = L("Value")
        value.width = 320
        value.minWidth = 120
        value.resizingMask = .autoresizingMask

        for column in showsSectionColumn ? [section, property, value] : [property, value] {
            tableView.addTableColumn(column)
        }

        tableView.dataSource = self
        tableView.delegate = self
        tableView.usesAlternatingRowBackgroundColors = true
        tableView.columnAutoresizingStyle = .lastColumnOnlyAutoresizingStyle
        tableView.gridStyleMask = .solidHorizontalGridLineMask
        tableView.allowsColumnResizing = true
        tableView.setAccessibilityLabel(L("Properties"))

        scrollView.documentView = tableView
        scrollView.hasVerticalScroller = true
        scrollView.autohidesScrollers = true
        scrollView.borderType = .bezelBorder
    }

    /// Re-reads every visible string after a language change.
    func applyLocalisedText() {
        for column in tableView.tableColumns {
            switch column.identifier {
            case Column.section: column.title = L("Section")
            case Column.property: column.title = L("Property")
            default: column.title = L("Value")
            }
        }
        tableView.setAccessibilityLabel(L("Properties"))
    }

    func show(_ properties: [AccessibilityProperty]) {
        self.properties = properties
        tableView.reloadData()
    }

    var displayedCount: Int { properties.count }

    // MARK: - Data source

    func numberOfRows(in tableView: NSTableView) -> Int { properties.count }

    func tableView(
        _ tableView: NSTableView,
        viewFor tableColumn: NSTableColumn?,
        row: Int
    ) -> NSView? {
        guard let tableColumn, row < properties.count else { return nil }
        let property = properties[row]

        let text: String
        switch tableColumn.identifier {
        case Column.section: text = property.group
        case Column.property: text = property.name
        default: text = property.value
        }

        let field: NSTextField
        if let reused = tableView.makeView(withIdentifier: tableColumn.identifier, owner: self)
            as? NSTextField {
            field = reused
        } else {
            field = NSTextField(wrappingLabelWithString: "")
            field.identifier = tableColumn.identifier
            field.isSelectable = true
            field.drawsBackground = false
            field.isBezeled = false
        }

        field.stringValue = text
        field.setAccessibilityLabel("\(property.group), \(property.name)")
        return field
    }

    /// Rows grow to fit the wrapped value so nothing is silently clipped.
    func tableView(_ tableView: NSTableView, heightOfRow row: Int) -> CGFloat {
        guard row < properties.count else { return 20 }
        let width = max(80, tableView.tableColumns.last?.width ?? 320) - 8
        let text = properties[row].value
        guard !text.isEmpty else { return 20 }

        let attributed = NSAttributedString(
            string: text,
            attributes: [.font: NSFont.systemFont(ofSize: NSFont.systemFontSize)])
        let bounds = attributed.boundingRect(
            with: NSSize(width: width, height: .greatestFiniteMagnitude),
            options: [.usesLineFragmentOrigin, .usesFontLeading])
        return max(20, ceil(bounds.height) + 6)
    }

    func tableViewColumnDidResize(_ notification: Notification) {
        // Wrapped heights depend on the value column width.
        tableView.noteHeightOfRows(
            withIndexesChanged: IndexSet(integersIn: 0..<max(properties.count, 1)))
    }
}
