import AppKit

/// Overlay colours, mirroring the Windows build's palette and its
/// high-contrast substitutions.
///
/// The documented meanings are load-bearing for users of the tool, so they are
/// kept identical across platforms: gold for sequential navigation, blue for
/// composite (arrow-key) navigation, green for the current focus stop.
enum OverlayPalette {

    static var increasedContrast: Bool {
        NSWorkspace.shared.accessibilityDisplayShouldIncreaseContrast
    }

    static var inspection: NSColor {
        increasedContrast ? .selectedContentBackgroundColor : NSColor.systemBlue
    }

    static var currentFocus: NSColor {
        increasedContrast ? .selectedContentBackgroundColor : NSColor.systemGreen
    }

    static var sequentialNavigation: NSColor {
        increasedContrast ? .controlAccentColor : NSColor.systemYellow
    }

    static var compositeNavigation: NSColor {
        increasedContrast ? .selectedContentBackgroundColor : NSColor.systemTeal
    }

    static var relationshipSource: NSColor {
        increasedContrast ? .selectedContentBackgroundColor : NSColor.systemRed
    }

    static var relationshipTarget: NSColor {
        increasedContrast ? .controlAccentColor : NSColor.systemTeal
    }

    /// Drawn underneath every stroke so connectors stay visible against any
    /// background the inspected application happens to be using.
    static var outline: NSColor {
        increasedContrast ? .white : NSColor.black
    }

    static var labelBackground: NSColor { .windowBackgroundColor }
    static var labelForeground: NSColor { .labelColor }
    static var labelBorder: NSColor {
        increasedContrast ? .selectedContentBackgroundColor : NSColor.separatorColor
    }

    static var ringThickness: CGFloat { increasedContrast ? 5 : 4 }
}
