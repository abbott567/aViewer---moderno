import Foundation

/// Runtime-switchable localisation.
///
/// The strings are compiled in so the app always has a complete English
/// catalogue, and any `<language>.lproj/Localizable.strings` file placed in the
/// bundle overrides or extends them. That keeps the Windows build's promise
/// that a language can be added by dropping in a resource file, without making
/// startup depend on a resource load succeeding.
final class Localization {

    static let shared = Localization()

    /// Languages offered in the View > Language menu.
    static let availableLanguages: [(code: String?, key: String)] = [
        (nil, "LanguageSystem"),
        ("en", "LanguageEnglish"),
        ("fr", "LanguageFrench")
    ]

    private var overrides: [String: String] = [:]
    private(set) var languageCode: String?

    private init() {}

    func setLanguage(_ code: String?) {
        languageCode = code
        overrides = Localization.loadOverrides(for: code ?? Locale.current.language.languageCode?.identifier)
    }

    func callAsFunction(_ key: String) -> String {
        overrides[key] ?? Localization.builtIn[key] ?? key
    }

    func format(_ key: String, _ arguments: CVarArg...) -> String {
        String(format: callAsFunction(key), arguments: arguments)
    }

    private static func loadOverrides(for code: String?) -> [String: String] {
        guard let code,
              let url = Bundle.main.url(
                forResource: "Localizable", withExtension: "strings",
                subdirectory: nil, localization: code),
              let contents = NSDictionary(contentsOf: url) as? [String: String]
        else { return [:] }
        return contents
    }

    // MARK: - Built-in catalogue

    private static let builtIn: [String: String] = [
        "AppTitle": "aViewer for macOS",
        "AboutApp": "About %@",
        "HideApp": "Hide %@",
        "QuitApp": "Quit %@",
        "HideOthers": "Hide Others",
        "ShowAll": "Show All",
        "MenuServices": "Services",
        "MenuEdit": "Edit",
        "MenuCopy": "Copy",
        "MenuSelectAll": "Select All",
        "MenuWindow": "Window",
        "MenuMinimise": "Minimise",
        "MenuZoom": "Zoom",
        "On": "on",
        "Off": "off",
        "PointerSwitchState": "Pointer inspection, %@. Activate to switch it.",
        "FocusSwitchState": "Keyboard focus inspection, %@. Activate to switch it.",
        "PointerAt": "Pointer: %1$d, %2$d",
        "SaveFailed": "Could not save the file: %@",
        "LanguageChanged": "Language changed",
        "EnhancedUserInterfaceTooltip": "Some applications change their layout when this is on. Leave it off unless an application exposes an incomplete tree without it.",
        "MenuFile": "File",
        "MenuCopyHtml": "Copy HTML",
        "MenuCopyHtmlSubtree": "Copy HTML subtree",
        "MenuCopyJson": "Copy JSON",
        "MenuSaveJson": "Save JSON…",
        "MenuInspect": "Inspect",
        "MenuInspectPointer": "Inspect element under pointer",
        "MenuInspectFocus": "Inspect element with keyboard focus",
        "MenuRecordFocusOrder": "Record focus order",
        "MenuStopRecordingFocusOrder": "Stop recording focus order",
        "MenuClearFocusPath": "Clear focus path",
        "MenuIncludeArrowNavigation": "Include arrow navigation",
        "MenuView": "View",
        "MenuShowRelationships": "Show relationships",
        "MenuAlwaysOnTop": "Always on top",
        "MenuEnhancedUserInterface": "Request enhanced user interface",
        "MenuChooseProperties": "Choose properties…",
        "MenuLanguage": "Language",
        "MenuNavigate": "Navigate",
        "MenuUpOneLevel": "Up one level",
        "MenuCompleteTree": "Complete application tree",
        "MenuHelp": "Help",
        "LanguageSystem": "System",
        "LanguageEnglish": "English",
        "LanguageFrench": "French",
        "HelpDocumentation": "Documentation",
        "HelpProjectWebsite": "Project website",
        "HelpReportIssue": "Report an issue",
        "HelpNoLinksConfigured": "No help links configured",
        "HelpInvalidLink": "That help link is not a valid web address.",
        "AccessibilityTree": "Accessibility tree",
        "ApiAccessibilityTree": "%@ accessibility tree",
        "Properties": "Properties",
        "TabPublished": "AX",
        "TabDerived": "ARIA (derived)",
        "TabDerivedCount": "ARIA (derived) — %d",
        "DerivedAriaCaveat": "Inferred by aViewer, not published by the application. macOS has no attribute for these states, so they are read from the role, subrole and value. Verified against Safari and WebKit only — treat results from other browser engines as unconfirmed, and check the source.",
        "DerivedAriaEmpty": "No ARIA states were inferred for this element. Only web content is examined, and only states macOS has no attribute of its own for — currently aria-pressed and aria-checked.",
        "Section": "Section",
        "Property": "Property",
        "Value": "Value",
        "TreeDepth": "Tree depth",
        "PointerInspect": "Pointer",
        "FocusInspect": "Keyboard focus",
        "FocusOrder": "Focus order",
        "StopFocusOrder": "Stop focus order",
        "Status": "Status",
        "Ready": "Ready",
        "PointerInspectionActive": "Pointer inspection is active. Press Escape to stop.",
        "FocusInspectionActive": "Keyboard focus inspection is active. Press Escape to stop.",
        "InspectionStopped": "Inspection stopped",
        "PointerSource": "Pointer",
        "KeyboardFocusSource": "Keyboard focus",
        "ParentSource": "Parent level",
        "CompleteTreeSource": "Complete application tree",
        "NoElementFound": "No element found from %@",
        "ElementSummary": "%1$@: %2$@ — %3$@",
        "InspectionFailed": "Inspection failed: %@",
        "SelectTreeElementFirst": "Select an element in the tree first",
        "InspectExternalElementFirst": "Inspect an element in another application first",
        "NoParentAvailable": "This element has no accessibility parent",
        "LoadingCompleteTree": "Loading the complete application tree…",
        "CompleteTreeUnavailable": "The complete application tree is unavailable",
        "CompleteTreeLoaded": "Loaded %1$@: %2$d elements",
        "CompleteTreeTruncated": "Loaded %1$@: %2$d elements (truncated at the node limit)",
        "JsonCopied": "Accessibility tree copied as JSON",
        "HtmlCopied": "Element copied as HTML",
        "HtmlSubtreeCopied": "Subtree copied as HTML",
        "SavedFile": "Saved %@",
        "DisplayingProperties": "Displaying %d selected properties",
        "RelationshipsEnabled": "Relationship visualisation enabled",
        "RelationshipsDisabled": "Relationship visualisation disabled",
        "AlwaysOnTopEnabled": "Always on top enabled",
        "AlwaysOnTopDisabled": "Always on top disabled",
        "ArrowNavigationEnabled": "Arrow-key focus transitions will be included",
        "ArrowNavigationDisabled": "Only Tab and Shift+Tab focus stops will be recorded",
        "RecordingTabOnly": "Recording Tab and Shift+Tab focus stops outside aViewer.",
        "RecordingWithArrows": "Recording Tab, Shift+Tab and arrow-key focus transitions outside aViewer.",
        "RecordingStoppedEmpty": "Focus-order recording stopped; no external focus stops were captured.",
        "RecordingStopped": "Focus-order recording stopped with %d stops.",
        "FocusPathCleared": "Focus-order path cleared",
        "FocusStop": "Focus stop %1$d: %2$@ — %3$@",
        "PermissionTitle": "aViewer needs accessibility access",
        "PermissionMessage": "aViewer reads other applications through the macOS accessibility API. Grant access in System Settings > Privacy & Security > Accessibility.\n\nIf aViewer is already switched on there, remove it with the minus button, then add it again — a rebuilt copy no longer matches the entry macOS recorded. If it is on and still refused, quit aViewer and open it again: macOS answers this question once per launch.",
        "PermissionOpenSettings": "Open System Settings",
        "PermissionRecheck": "Check again",
        "PermissionMissingStatus": "Accessibility access has not been granted. Inspection is unavailable.",
        "PermissionGrantedStatus": "Accessibility access granted. Inspection is available.",
        "PermissionNeedsRelaunch": "Accessibility access still unavailable. macOS checks this once per launch, so if the toggle is already on, quit aViewer and open it again.",
        "ChoosePropertiesTitle": "Choose properties",
        "ChoosePropertiesDescription": "Clear a property to hide it from the property grid. The list grows as new attributes are encountered, so inspect a few applications to see everything they publish.",
        "SelectAll": "Select all",
        "SelectNone": "Select none",
        "FilterProperties": "Filter properties",
        "Show": "Show",
        "ShowProperty": "Show %@",
        "Apply": "Apply",
        "Cancel": "Cancel",
        "EnhancedUserInterfaceOn": "Enhanced user interface requested for inspected applications",
        "EnhancedUserInterfaceOff": "Enhanced user interface no longer requested",
        "TreeTruncated": "Tree truncated at %d elements"
    ]
}

/// Shorthand used throughout the UI layer.
func L(_ key: String) -> String { Localization.shared(key) }
func LF(_ key: String, _ arguments: CVarArg...) -> String {
    String(format: Localization.shared(key), arguments: arguments)
}
