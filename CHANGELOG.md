# Changelog

## 2026.07.29.8

- Restored numbered focus-order stops. Number badges are positioned outside element bounds.
- Added a complete UIA, MSAA and IAccessible2 property catalogue to Preferences.
- Added **Select all API properties** and an option to show selected properties as **Not exposed** when an API returns no value.
- Added **Navigate > Load complete app or page tree** and a toolbar action.
- Added complete-tree expansion and collapse commands.
- Added an **All** tree-depth option.
- Complete-tree inspection resolves the enclosing web document where available, otherwise the target application's accessibility root.

## 2026.07.29.7 — table-cell relationship extraction

- Added UIA table-cell relationship extraction through `TableItemPattern.RowHeaderItemsProperty`, `ColumnHeaderItemsProperty`, `GetRowHeaderItems()` and `GetColumnHeaderItems()`.
- Added IA2 table-cell relationship extraction through `IAccessibleTableCell::rowHeaderCells` and `columnHeaderCells`.
- Added UIA and IA2 table-cell metadata to the property panel, including row, column and span information.
- Made relationship rows always visible in the property panel, regardless of saved property-filter choices.
- Made IA2 relationship deduplication type-aware so distinct row-header and column-header relationships to the same target are retained.
- Routed the recovered table header relationships through the existing target-bound resolver and relationship overlay.

## 2026.07.29.6 — gap-only routing, relationship table restoration and inspect switches

- Replaced centre-to-centre relationship connectors with side-aware orthogonal routing.
- Targets below a source now connect from the source bottom border to the target top border; equivalent facing-border rules apply above, left and right.
- Connector shafts, line caps and arrowheads remain in the gap between elements and no longer cover source or target content.
- Applied the same route construction and arrow renderer to focus-order visualization.
- Restored relationship information to the property tables, including merged cross-API targets, source API/property, target identity, role/name and drawable bounds.
- Restored Pointer inspect and Focus inspect as compact, keyboard-focus-visible toggle switches using Windows system colours.
- Retained source and target borders, multi-target enumeration, menu UI and prior build corrections.

## 2026.07.29.5 — file-system namespace and property-dialog API correction

- Added explicit `System.IO` imports to `AppSettingsService.cs`, `HelpMenuLinkService.cs`, and `PropertyFilterService.cs`.
- Added `PropertySelectionWindow.AllChoices`, matching the existing `MainWindow` call and preserving selections outside the current search filter.
- Updated source validation to detect missing file-system imports, malformed accessor semicolons, duplicate type declarations, missing XAML handlers, and the property-dialog API mismatch.
- Updated `build.ps1` to run source validation before restore and compilation.
- Retained the persistent menu and all relationship and focus-order visualization changes.

## 2026.07.29.4 — duplicate-type build correction

- Renamed `AccessibilityVisualPalette` to `AViewerOverlayPalette` so legacy source files left by an in-place extraction cannot collide with the current implementation.
- Renamed the internal `HelpMenuLink` data type to `ConfiguredHelpMenuEntry` for the same reason.
- Excluded the obsolete `AccessibilityVisualPalette.cs` and `HelpMenuLink.cs` filenames from compilation if they remain in an existing checkout.
- Updated `build.ps1` to remove known obsolete source files and all stale `bin` and `obj` directories before restoring and building.
- Retained the restored menu UI and all relationship and focus-order visualization fixes.

## 2026-07-29 — Build correction v2

- Fully qualified both UI Automation point constructions as `System.Windows.Point`.
- Fully qualified empty UI Automation rectangles as `System.Windows.Rect.Empty`.
- Removed unused `UseWindowsForms` from `AViewer.Core` to eliminate the imported `System.Drawing.Point` type.


## 2026-07-29 — .NET build correction

- Fixed `CS0104` in `Uia3Inspector.cs` by explicitly aliasing `System.Windows.Point` and `System.Windows.Rect`.
- Removed redundant direct `UIAutomationClient` and `UIAutomationTypes` assembly references; the Windows Desktop WPF framework reference supplies them.
- Prevented the `UIAutomationTypes` assembly-resolution conflict reported when building with .NET SDK 10.0.302.
- Added source checks for the UI Automation type aliases and project-reference configuration.
- Preserved the restored menu UI and all relationship and focus-order visualization changes.

## 2026-07-29 — menu restoration

- Restored the persistent application menu bar above the toolbar.
- Retained the File, Inspect, View, Navigate and configurable Help menus.
- Reserved a minimum menu-bar height so the content area cannot collapse over it.
- Replaced fragile menu-specific system brushes with platform control colours that remain visible in normal, dark and high-contrast configurations.
- Added an accessible name to the application menu.
- Preserved all relationship and focus-order visualization changes.

## 2026-07-29

- Integrated multi-target IA2 relationship discovery.
- Recovered missing visible target bounds from the current UIA, MSAA and IA2 snapshot trees.
- Ensured relationship source and every drawable target receive borders.
- Anchored relationship arrows to source and target borders.
- Removed all relationship labels.
- Removed focus-order numbers and key labels.
- Unified relationship and focus-order arrow rendering.
- Added clearly defined outlined arrowheads.
- Added short-connector routing for adjacent inline targets.
- Packaged the change as a complete solution; no patch script is required.

## 2026.07.29.3

- Fixed `CS1597` in `PropertySelectionWindow.xaml.cs` by removing the invalid semicolon after the `Choices` auto-property accessor block.
- Retained the restored menu UI and relationship/focus-order visualization fixes.
