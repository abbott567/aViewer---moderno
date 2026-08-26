import Foundation

/// The key press that produced a focus transition.
enum FocusNavigationKey {
    case tab
    case shiftTab
    case arrowLeft
    case arrowRight
    case arrowUp
    case arrowDown

    /// Arrow keys move focus inside composite widgets — tab lists, menus,
    /// grids, radio groups — rather than through the sequential tab order, and
    /// are drawn in a different colour to make that distinction visible.
    var isArrow: Bool {
        switch self {
        case .tab, .shiftTab: return false
        default: return true
        }
    }

    var label: String {
        switch self {
        case .tab: return "Tab"
        case .shiftTab: return "Shift+Tab"
        case .arrowLeft: return "Left arrow"
        case .arrowRight: return "Right arrow"
        case .arrowUp: return "Up arrow"
        case .arrowDown: return "Down arrow"
        }
    }
}

/// One recorded stop on the focus path.
struct FocusOrderStep {
    let sequence: Int
    let navigationKey: FocusNavigationKey
    let element: AccessibilityNode
}
