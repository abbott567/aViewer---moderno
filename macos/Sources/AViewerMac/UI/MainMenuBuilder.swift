import AppKit

/// Builds the menu bar.
///
/// The commands match the Windows build one for one. Two shortcuts differ
/// deliberately: Copy JSON is Shift-Command-C rather than Command-C, and Copy
/// HTML subtree takes Option, so that plain Command-C keeps its standard
/// meaning of copying the selected text out of the property grid.
enum MainMenuBuilder {

    static func rebuild(for controller: MainWindowController) {
        let menu = NSMenu()
        menu.addItem(applicationMenu())
        menu.addItem(fileMenu(controller))
        menu.addItem(editMenu())
        menu.addItem(inspectMenu(controller))
        menu.addItem(viewMenu(controller))
        menu.addItem(navigateMenu(controller))
        menu.addItem(windowMenu())
        menu.addItem(helpMenu(controller))
        NSApp.mainMenu = menu
    }

    // MARK: - Menus

    private static func applicationMenu() -> NSMenuItem {
        let name = L("AppTitle")
        let submenu = NSMenu(title: name)
        submenu.addItem(withTitle: LF("AboutApp", name),
                        action: #selector(NSApplication.orderFrontStandardAboutPanel(_:)),
                        keyEquivalent: "")
        submenu.addItem(.separator())

        let services = NSMenuItem(title: L("MenuServices"), action: nil, keyEquivalent: "")
        let servicesMenu = NSMenu()
        services.submenu = servicesMenu
        NSApp.servicesMenu = servicesMenu
        submenu.addItem(services)
        submenu.addItem(.separator())

        submenu.addItem(withTitle: LF("HideApp", name),
                        action: #selector(NSApplication.hide(_:)), keyEquivalent: "h")
        let hideOthers = NSMenuItem(
            title: L("HideOthers"),
            action: #selector(NSApplication.hideOtherApplications(_:)),
            keyEquivalent: "h")
        hideOthers.keyEquivalentModifierMask = [.command, .option]
        submenu.addItem(hideOthers)
        submenu.addItem(withTitle: L("ShowAll"),
                        action: #selector(NSApplication.unhideAllApplications(_:)),
                        keyEquivalent: "")
        submenu.addItem(.separator())
        submenu.addItem(withTitle: LF("QuitApp", name),
                        action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")

        let item = NSMenuItem()
        item.submenu = submenu
        return item
    }

    private static func fileMenu(_ controller: MainWindowController) -> NSMenuItem {
        let submenu = NSMenu(title: L("MenuFile"))
        add(submenu, L("MenuCopyHtml"), #selector(MainWindowController.copyHTML(_:)),
            controller, "H", [.command, .shift])
        add(submenu, L("MenuCopyHtmlSubtree"),
            #selector(MainWindowController.copyHTMLSubtree(_:)),
            controller, "H", [.command, .shift, .option])
        submenu.addItem(.separator())
        add(submenu, L("MenuCopyJson"), #selector(MainWindowController.copyJSON(_:)),
            controller, "C", [.command, .shift])
        add(submenu, L("MenuSaveJson"), #selector(MainWindowController.saveJSON(_:)),
            controller, "s", [.command])
        return wrap(submenu)
    }

    /// Standard editing commands so text can be selected and copied out of the
    /// property grid the way it can anywhere else on the system.
    private static func editMenu() -> NSMenuItem {
        let submenu = NSMenu(title: L("MenuEdit"))
        submenu.addItem(withTitle: L("MenuCopy"),
                        action: #selector(NSText.copy(_:)), keyEquivalent: "c")
        submenu.addItem(withTitle: L("MenuSelectAll"),
                        action: #selector(NSText.selectAll(_:)), keyEquivalent: "a")
        return wrap(submenu)
    }

    private static func inspectMenu(_ controller: MainWindowController) -> NSMenuItem {
        let submenu = NSMenu(title: L("MenuInspect"))
        add(submenu, L("MenuInspectPointer"),
            #selector(MainWindowController.togglePointerInspection(_:)),
            controller, "P", [.command, .shift])
        add(submenu, L("MenuInspectFocus"),
            #selector(MainWindowController.toggleFocusInspection(_:)),
            controller, "F", [.command, .shift])
        submenu.addItem(.separator())
        add(submenu, L("MenuRecordFocusOrder"),
            #selector(MainWindowController.toggleFocusOrderRecording(_:)),
            controller, "O", [.command, .shift])
        add(submenu, L("MenuClearFocusPath"),
            #selector(MainWindowController.clearFocusPath(_:)), controller)

        let arrows = add(submenu, L("MenuIncludeArrowNavigation"),
                         #selector(MainWindowController.toggleArrowNavigation(_:)), controller)
        arrows.state = controller.isArrowNavigationIncluded ? .on : .off
        return wrap(submenu)
    }

    private static func viewMenu(_ controller: MainWindowController) -> NSMenuItem {
        let submenu = NSMenu(title: L("MenuView"))

        let relationships = add(submenu, L("MenuShowRelationships"),
                                #selector(MainWindowController.toggleRelationships(_:)), controller)
        relationships.state = controller.isRelationshipsEnabled ? .on : .off

        let onTop = add(submenu, L("MenuAlwaysOnTop"),
                        #selector(MainWindowController.toggleAlwaysOnTop(_:)), controller)
        onTop.state = controller.isAlwaysOnTop ? .on : .off

        let enhanced = add(submenu, L("MenuEnhancedUserInterface"),
                           #selector(MainWindowController.toggleEnhancedUserInterface(_:)),
                           controller)
        enhanced.state = controller.isEnhancedUserInterfaceEnabled ? .on : .off
        enhanced.toolTip = L("EnhancedUserInterfaceTooltip")

        submenu.addItem(.separator())
        add(submenu, L("MenuChooseProperties"),
            #selector(MainWindowController.chooseProperties(_:)), controller)
        submenu.addItem(.separator())

        let language = NSMenuItem(title: L("MenuLanguage"), action: nil, keyEquivalent: "")
        let languageMenu = NSMenu(title: L("MenuLanguage"))
        for entry in Localization.availableLanguages {
            let item = add(languageMenu, L(entry.key),
                           #selector(MainWindowController.changeLanguage(_:)), controller)
            item.representedObject = entry.code
            item.state = entry.code == controller.currentLanguage ? .on : .off
        }
        language.submenu = languageMenu
        submenu.addItem(language)

        return wrap(submenu)
    }

    private static func navigateMenu(_ controller: MainWindowController) -> NSMenuItem {
        let submenu = NSMenu(title: L("MenuNavigate"))
        let up = add(submenu, L("MenuUpOneLevel"),
                     #selector(MainWindowController.upOneLevel(_:)), controller,
                     String(UnicodeScalar(NSUpArrowFunctionKey)!), [.command])
        up.setAccessibilityLabel(L("MenuUpOneLevel"))
        add(submenu, L("MenuCompleteTree"),
            #selector(MainWindowController.loadCompleteTree(_:)),
            controller, "T", [.command, .shift])
        return wrap(submenu)
    }

    private static func windowMenu() -> NSMenuItem {
        let submenu = NSMenu(title: L("MenuWindow"))
        submenu.addItem(withTitle: L("MenuMinimise"),
                        action: #selector(NSWindow.performMiniaturize(_:)), keyEquivalent: "m")
        submenu.addItem(withTitle: L("MenuZoom"),
                        action: #selector(NSWindow.performZoom(_:)), keyEquivalent: "")
        let item = wrap(submenu)
        NSApp.windowsMenu = submenu
        return item
    }

    private static func helpMenu(_ controller: MainWindowController) -> NSMenuItem {
        let submenu = NSMenu(title: L("MenuHelp"))
        let links = HelpMenuLinkService.load()

        if links.isEmpty {
            let empty = submenu.addItem(
                withTitle: L("HelpNoLinksConfigured"), action: nil, keyEquivalent: "")
            empty.isEnabled = false
        }

        for link in links {
            if link.isSeparator == true {
                submenu.addItem(.separator())
                continue
            }
            guard HelpMenuLinkService.isAllowedURL(link.url) else { continue }
            let title = link.resourceKey.map { L($0) } ?? link.label ?? link.url ?? ""
            let item = add(submenu, title,
                           #selector(MainWindowController.openHelpLink(_:)), controller)
            item.representedObject = link.url
            item.toolTip = link.url
        }

        let menuItem = wrap(submenu)
        NSApp.helpMenu = submenu
        return menuItem
    }

    // MARK: - Helpers

    @discardableResult
    private static func add(
        _ menu: NSMenu,
        _ title: String,
        _ action: Selector,
        _ target: AnyObject,
        _ keyEquivalent: String = "",
        _ modifiers: NSEvent.ModifierFlags = []
    ) -> NSMenuItem {
        // A capital letter in a key equivalent implies Shift on macOS; the
        // modifier mask is set explicitly so the displayed shortcut is right.
        let item = NSMenuItem(
            title: title, action: action, keyEquivalent: keyEquivalent.lowercased())
        if !keyEquivalent.isEmpty { item.keyEquivalentModifierMask = modifiers }
        item.target = target
        menu.addItem(item)
        return item
    }

    private static func wrap(_ submenu: NSMenu) -> NSMenuItem {
        let item = NSMenuItem(title: submenu.title, action: nil, keyEquivalent: "")
        item.submenu = submenu
        return item
    }
}
