# AViewer Modern

A modern Windows accessibility API inspection tool inspired by the original aViewer.

## Current implementation

- UI Automation 3 inspection using FlaUI
- Inspect the element under the pointer
- Inspect the element with keyboard focus
- Configurable descendant depth
- Accessible tree and property-grid interface
- UIA properties, ARIA role/properties and supported control patterns
- Copy or save the captured tree as JSON
- .NET 8, nullable reference types and warning-free builds
- Unit-test project

## Technology

- .NET 8
- WPF
- [FlaUI](https://github.com/FlaUI/FlaUI), MIT licensed
- CommunityToolkit.Mvvm, MIT licensed

WPF is used deliberately: it is mature, open source, works well with Windows accessibility APIs, has good keyboard support, and avoids introducing a browser runtime into an inspection tool.

## Build on Windows

Install Visual Studio 2022 with the **.NET desktop development** workload and the .NET 8 SDK, then run:

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet run --project .\src\AViewer.App\AViewer.App.csproj
```

Publish a self-contained 64-bit executable:

```powershell
dotnet publish .\src\AViewer.App\AViewer.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true
```

## Architecture

`AViewer.Core` contains API adapters and neutral data models. `AViewer.App` contains only the WPF presentation layer. Additional adapters can implement `IAccessibilityInspector` without changing the UI.

## Compatibility roadmap

The original aViewer exposes MSAA, IAccessible2, UI Automation, ARIA and browser DOM information. This rebuild starts with UIA3 because it is the current Windows accessibility API and because Chromium and modern Firefox expose useful ARIA information through UIA.

Planned adapters:

1. **MSAA** via `oleacc.dll`, for legacy Win32 applications.
2. **IAccessible2** via generated COM interop definitions, for Firefox and other IA2 providers.
3. **UIA event monitor** for focus, property and structure-change events.
4. **Highlight overlay** that does not steal focus.
5. **Snapshot comparison** and HTML/Markdown export.
6. **Plugin API** for custom property panels and browser-specific diagnostics.

## Licensing

New code in this package is provided under Apache-2.0. Verify the original repository's branch-specific licensing and attribution before copying any original implementation code. This project is a clean architectural rebuild and does not copy the Delphi implementation.

## Inspection modes

- **Inspect pointer** starts continuous global pointer inspection. Move the pointer over another application and AViewer updates when the UI Automation element changes.
- **Inspect focus** starts continuous keyboard-focus inspection. Move focus in another application and AViewer updates automatically.
- AViewer ignores elements from its own process, remains visible, and does not minimise itself.
- Press the active inspection button again, or press `Escape` while AViewer has focus, to stop.


## Inspection focus ring

While pointer or keyboard-focus inspection is active, AViewer draws a non-interactive topmost ring around the current external UI Automation element. The ring is click-through, does not take keyboard focus, and is removed when inspection stops or AViewer closes.

## IAccessible2 support

The inspector now queries the native MSAA object at the inspected screen point through `AccessibleObjectFromPoint`, then requests the `IAccessible2` COM interface from the returned object. The property grid keeps UIA, MSAA and IA2 values in separate API groups.

IA2 information is available for applications that expose it, including Chromium-based browsers, Firefox and IA2-enabled desktop applications. Applications that expose only UIA or MSAA show `IA2 / Available: False`.

Currently reported IA2 values include role, states, object attributes, extended role, unique ID, window handle, index in parent, relation count, extended-state count, group position and locale. This first IA2 layer does not yet enumerate IA2 relations, text runs, hyperlinks or tables.

## IA2 acquisition fix

IAccessible2 is acquired using the browser-compatible COM path:

1. `AccessibleObjectFromPoint` obtains the MSAA `IAccessible` object.
2. Direct `QueryInterface(IID_IAccessible2)` is attempted.
3. If that fails, `IServiceProvider::QueryService(IID_IAccessible, IID_IAccessible2)` is used.

The property grid includes an **IA2 / Acquisition** row showing which path succeeded or the HRESULTs returned when acquisition failed.

## IA2 crash-safety correction

This build does not project `IAccessible2` as an inherited managed COM interface. It acquires the native IA2 pointer with `QueryInterface`/`IServiceProvider::QueryService`, calls the official IA2 vtable slots directly, frees returned BSTR values, and balances each acquired interface pointer with one `Marshal.Release`. It deliberately avoids `Marshal.FinalReleaseComObject`, which can invalidate shared runtime-callable wrappers during continuous inspection.

## Human-readable MSAA and IA2 output

This build resolves standard MSAA roles and states with `GetRoleTextW` and
`GetStateTextW`. IA2 roles are displayed using their defined names. Because
`IAccessible2::role` may return either an IA2-specific role or a standard MSAA
role, both are handled. If an IA2 provider does not return a role, the UI shows
the MSAA role as an explicitly labelled fallback rather than an empty or raw
numeric value.

## Property display settings

Use **Choose properties…** to select which UIA, MSAA and IAccessible2 properties are shown in the property grid. The dialog supports search, **Select all**, and **Select none**. Choices are stored in `%LOCALAPPDATA%\AViewerModern\property-filter.json` and restored on the next run.

## Always on top

Select **Always on top** in the main toolbar to keep AViewer above other application windows while inspecting. The setting is stored in `%LOCALAPPDATA%\AViewerModern\app-settings.json` and restored on the next run.

## UIA tables and relationships

The inspector reports UI Automation Grid, Table, GridItem and TableItem pattern data, including row and column counts, cell coordinates and spans, containing grid, table orientation, and associated row and column header elements.

Enable **Show relationships** to draw a click-through overlay between the selected element and UIA-related elements. Supported relationships include LabeledBy, DescribedBy, ControllerFor, FlowsTo, FlowsFrom, GridItem containing grid, and TableItem row/column headers. These are the platform accessibility relationships commonly produced by HTML `aria-labelledby`, `aria-describedby`, `aria-controls`, `aria-flowto`, and table `headers` associations.

## API property tabs

The properties panel separates UI Automation, MSAA, and IAccessible2 output into dedicated tabs. Property visibility choices continue to apply within each tab.

The relationship overlay does not include a containing-grid or "Contained by" connector. Containing-grid information remains available as a UIA table-item property.

## IAccessible2 relationships and tables

The IA2 inspector now enumerates `IAccessible2::relation` and visualizes non-containment relation targets in the existing relationship overlay. IA2 relationship rows are shown in the IAccessible2 tab. Containment-style relations remain excluded.

Supported IA2 relationship labels include labelled by, described by, controller for, controlled by, flows to/from, details, error message, member of, and table row/column headers.

For tables, AViewer queries `IAccessibleTable2` and `IAccessibleTableCell`. Table properties include row and column counts and selection counts. Cell properties include row, column, spans, selected state, containing table, and row/column header cells. Header cells are also added to the relationship visualization.

## API-specific accessibility trees

The tree panel follows the selected properties tab:

- **UIA** shows the UI Automation tree.
- **MSAA** shows the Microsoft Active Accessibility tree.
- **IAccessible2** shows the IA2-enhanced accessible tree.

Each tree is captured from the same inspected screen point or focused element and uses the selected depth. Selecting a node updates the property grid and relationship overlay for that API.

## Keyboard-focus visualization

Enable **Show keyboard focus** to draw a green dashed, click-through ring around the element that currently has keyboard focus. This visualization is independent of the blue inspected-element ring and remains active when pointer inspection is running or inspection is stopped. The setting is persisted in `%LOCALAPPDATA%\AViewerModern\app-settings.json`.

## Focus-order visualization

Select **Record focus order**, move to the application under test, and navigate with
Tab or Shift+Tab. AViewer records a stop only when the accessible focus target
changes. The overlay numbers each stop and draws labelled arrows between them.

Enable **Include arrow navigation** to also record focus transitions caused by the
Left, Right, Up and Down Arrow keys. This is intended for composite widgets such as
tab lists, menus, grids, tree views, radio groups and list boxes. Arrow presses that
do not change the accessible focus target are not added as stops.

- Gold arrows: Tab or Shift+Tab transitions.
- Blue arrows: arrow-key transitions inside widgets.
- Green outline: the latest recorded focus stop.
- **Clear focus path** removes the recorded path.
- Escape stops recording without clearing the path.

## Copy HTML

The **Copy HTML** and **Copy HTML subtree** commands create an HTML representation from the selected accessibility node and the accessibility metadata exposed by UIA or IAccessible2. The output uses exposed HTML tag, ID, class, ARIA role and ARIA properties when available, and falls back to semantic tags inferred from the accessible role.

This is not a direct browser DOM dump. Windows accessibility APIs do not generally expose `outerHTML`. Exact source DOM capture would require a browser-specific integration such as the Chrome DevTools Protocol, a Firefox remote-debugging adapter, or a browser extension.

## Move up the accessibility tree

Select a node and choose **Up one level**. AViewer resolves the element at the selected node's screen bounds, moves to its accessibility parent, and rebuilds the UIA, MSAA and IAccessible2 trees using the current depth setting. Parent resolution is best effort because providers can expose overlapping or virtual elements with identical bounds.

## Localisation

The application uses `.resx` resources with live bindings and culture persistence. English is the neutral language and a French sample is included. See `LOCALIZATION.md` for adding languages, placeholders, access keys, and right-to-left testing.
## Configurable Help menu links

Help menu links are loaded from `src/AViewer.App/HelpMenuLinks.json`. See `HELP_MENU_LINKS.md` for adding localised links, literal labels, separators, and deployment-specific support links without changing XAML.


## Multi-target relationship visualisation

Relationship visualisation merges equivalent nodes across UIA, MSAA and IAccessible2 before drawing the overlay. This is important for relationships such as `aria-labelledby` with multiple ID references: UIA may expose only one `LabeledBy` target, while IAccessible2 can expose every `labelledBy` target. The overlay preserves each distinct target and draws a separate connector to each referenced element, regardless of which API tab is active.

## Multi-target IA2 relationships

Relationship extraction now uses `IAccessible2_2::relationTargetsOfType` when available. This preserves every target in relationships such as a multi-ID `aria-labelledby`, while retaining the original `IAccessibleRelation` enumeration as a fallback.

## Complete application tree

Inspect an element in the target application, then choose **Navigate > Complete application tree** or press **Ctrl+Shift+T**. AViewer uses the last externally inspected element, walks to the highest ancestor in the same process, and loads that application subtree. It does not query keyboard focus after the command is activated, so opening the menu cannot redirect the operation to AViewer itself.
