# AViewer Modern

## Installation and usage guide

AViewer Modern is a Windows accessibility inspection tool that exposes information from:

- Microsoft UI Automation (UIA)
- Microsoft Active Accessibility (MSAA / IAccessible)
- IAccessible2 (IA2)

It also provides:

- Continuous pointer inspection
- Continuous keyboard-focus inspection
- API-specific accessibility trees
- Element highlighting
- Accessibility relationship visualisation
- Focus-order recording and visualisation
- UIA and IA2 table information
- Configurable property visibility
- Persistent application settings
- Windows High Contrast, system colour, font, DPI, and keyboard-cue support

## 1. System requirements

### Required

- Windows 10 or Windows 11
- 64-bit Windows recommended
- .NET 8 SDK
- PowerShell 5.1 or later, or PowerShell 7
- Permission to run locally built applications

### Recommended

- Visual Studio 2022 Community or later
- The **.NET desktop development** workload
- A display configuration matching the applications you intend to inspect
- Administrator privileges when inspecting an application that is running as Administrator

AViewer cannot inspect an elevated application from a non-elevated AViewer process because Windows prevents lower-integrity processes from accessing some higher-integrity UI information.

## 2. Download and extract the project

Download the current project ZIP and extract it into a new empty directory.

Do not copy new versions over an older build. Older XAML and C# files may remain in the project and cause duplicate class or duplicate control errors.

Example:

```powershell
Expand-Archive `
  E:\aviewer-modern-platform-accessibility.zip `
  E:\aviewer-modern-platform-accessibility `
  -Force
```

The extracted project may contain an additional top-level folder. Locate the solution or application project with:

```powershell
Get-ChildItem E:\aviewer-modern-platform-accessibility `
  -Recurse `
  -Filter AViewer.App.csproj
```

Change directory to the folder containing the `src` directory.

Example:

```powershell
cd E:\aviewer-modern-platform-accessibility\aviewer-propfilter
```

Confirm that the application project exists:

```powershell
Test-Path .\src\AViewer.App\AViewer.App.csproj
```

The result should be:

```text
True
```

## 3. Install the .NET 8 SDK

Check whether .NET is already installed:

```powershell
dotnet --info
```

The output should list a .NET 8 SDK.

You can also list installed SDKs:

```powershell
dotnet --list-sdks
```

A suitable result resembles:

```text
8.0.xxx
```

If .NET 8 is not installed, install either:

- The .NET 8 SDK, or
- Visual Studio 2022 with the **.NET desktop development** workload

Restart PowerShell after installation.

## 4. Build the application

From the project root:

```powershell
dotnet restore
dotnet build -c Debug
```

To build a release version:

```powershell
dotnet build -c Release
```

## 5. Run the application

From the project root:

```powershell
dotnet run --project .\src\AViewer.App\AViewer.App.csproj
```

If AViewer needs to inspect an application running as Administrator:

1. Close AViewer.
2. Open PowerShell as Administrator.
3. Run the same command again.

## 6. Run from Visual Studio

1. Open `AViewer.sln` in Visual Studio.
2. Allow NuGet package restoration to complete.
3. In Solution Explorer, right-click `AViewer.App`.
4. Choose **Set as Startup Project**.
5. Press `F5` to run with debugging.
6. Press `Ctrl+F5` to run without debugging.

## 7. Publish a standalone build

A self-contained build includes the .NET runtime.

From the project root:

```powershell
dotnet publish .\src\AViewer.App\AViewer.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true
```

The published application will normally be in:

```text
src\AViewer.App\bin\Release\net8.0-windows\win-x64\publish\
```

Run:

```powershell
.\src\AViewer.App\bin\Release\net8.0-windows\win-x64\publish\AViewer.App.exe
```

For a smaller framework-dependent build:

```powershell
dotnet publish .\src\AViewer.App\AViewer.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false
```

The target machine must have the compatible .NET Desktop Runtime installed.

## 8. Main interface

The main window contains:

- Inspection controls
- API tabs
- Accessibility tree
- Property panel
- Property selection controls
- Relationship visualisation controls
- Focus-order recording controls
- Export controls
- Always-on-top setting

The selected API tab controls both:

- Which accessibility tree is displayed
- Which API-specific properties are displayed

Available tabs:

- UIA
- MSAA
- IAccessible2

## 9. Pointer inspection

Pointer inspection continuously inspects the element beneath the mouse pointer.

### Start pointer inspection

1. Select **Inspect pointer**.
2. Move the pointer outside AViewer.
3. Pause over a control or document element.
4. AViewer updates the tree and property panel.
5. The inspected element is highlighted.

### Stop pointer inspection

Select **Stop pointer inspection**, or press `Escape` when supported by the current build.

### Notes

- AViewer ignores its own controls.
- Some applications expose different elements depending on the selected API.
- Browser accessibility trees may take a moment to initialise.
- The same element may be refreshed repeatedly so late-populated IA2 properties can appear.

## 10. Keyboard-focus inspection

Keyboard-focus inspection follows the element that currently owns system keyboard focus.

### Start focus inspection

1. Select **Inspect focus**.
2. Move to another application with `Alt+Tab`.
3. Navigate using `Tab`, `Shift+Tab`, arrow keys, or application-specific keys.
4. AViewer updates when focus changes.

### Stop focus inspection

Select **Stop focus inspection**, or press `Escape` when supported.

### Important

Clicking AViewer moves focus into AViewer. Use keyboard navigation to return to the application under test.

## 11. API-specific accessibility trees

The tree changes when you choose a different API tab.

### UIA tree

Displays the Microsoft UI Automation hierarchy.

Useful for:

- Native Windows applications
- WPF
- WinUI
- UWP
- Modern Chromium accessibility through UIA
- UIA control patterns and properties

### MSAA tree

Displays the Microsoft Active Accessibility hierarchy.

Useful for:

- Legacy Win32 applications
- Older desktop controls
- Applications that expose IAccessible but not rich UIA data

### IAccessible2 tree

Displays the IA2-enhanced accessibility hierarchy.

Useful for:

- Chrome
- Edge
- Firefox
- Document-oriented applications
- Applications exposing IA2 roles, states, attributes, relations, tables, and text semantics

Changing API tabs changes both the visible tree and the property grid.

## 12. Selecting tree nodes

Selecting a node in the accessibility tree:

- Displays the node’s API-specific properties
- Moves the inspection highlight to that node
- Updates the relationship visualisation
- Updates JSON copy and export data
- Updates table information where available

The inspected pointer or focus target may be the tree root, while descendants appear below it according to the selected depth.

## 13. Tree depth

Use the depth control to limit how much of the accessibility hierarchy is loaded.

A lower depth:

- Updates faster
- Uses less memory
- Is suitable for large browser documents

A higher depth:

- Exposes more descendants
- May be slower
- Can be expensive for complex web pages, grids, trees, and documents

Start with a depth of 2 or 3. Increase it only when needed.

## 14. UIA properties

The UIA tab may expose:

- Name
- Control type
- Localised control type
- Automation ID
- Class name
- Framework ID
- Process ID
- Runtime ID
- Bounding rectangle
- Enabled state
- Keyboard-focus state
- Keyboard-focusable state
- Offscreen state
- Password state
- Content-element state
- Control-element state
- Help text
- Access key
- Accelerator key
- ARIA role
- ARIA properties
- Supported control patterns

## 15. UIA table properties

When supported by the target, the UIA tab exposes table and grid information from:

- Grid pattern
- Table pattern
- GridItem pattern
- TableItem pattern

Possible properties include:

- Row count
- Column count
- Row-or-column-major orientation
- Row headers
- Column headers
- Cell row
- Cell column
- Row span
- Column span
- Containing grid
- Row header items
- Column header items

Not every table provider exposes every property.

HTML tables may expose different information through UIA and IA2.

## 16. MSAA properties

The MSAA tab may expose inherited IAccessible information such as:

- Name
- Role
- State
- Value
- Description
- Help
- Keyboard shortcut
- Default action
- Child count
- Location
- Parent and child information

Roles and states are displayed as readable names rather than only numeric or hexadecimal values.

## 17. IAccessible2 properties

The IA2 tab may expose:

- Name inherited from `IAccessible::accName`
- IA2 role
- IA2 role identifier
- IA2 states
- Object attributes
- Extended role
- Localised extended role
- Unique ID
- Window handle
- Index in parent
- Relation count
- Extended-state count
- Group position
- Locale

The accessible name is inherited from MSAA. IA2 does not define a separate name getter.

## 18. IA2 table properties

When the target exposes `IAccessibleTable2` or `IAccessibleTableCell`, AViewer may display:

### Table properties

- Row count
- Column count
- Selected cell count
- Selected row count
- Selected column count
- Caption
- Summary

### Cell properties

- Row
- Column
- Row span
- Column span
- Selected state
- Row and column extents
- Containing table
- Row header cells
- Column header cells

These properties depend on the target application correctly exposing IA2 table interfaces.

## 19. Accessibility relationships

AViewer can expose and visualise relationships between accessible elements.

### UIA relationships

Possible relationships include:

- Labeled by
- Described by
- Controller for
- Flows to
- Flows from
- Row header
- Column header

These may represent platform mappings for relationships created by:

- `aria-labelledby`
- `aria-describedby`
- `aria-controls`
- `aria-flowto`
- HTML table header associations

### IA2 relationships

Possible IA2 relationships include:

- Labeled by
- Label for
- Described by
- Description for
- Controller for
- Controlled by
- Flows to
- Flows from
- Details
- Error message
- Member of
- Popup for
- Row header
- Column header

Containment relationships such as “contained by” are intentionally excluded from the relationship visualisation.

## 20. Relationship visualisation

Enable **Show relationships** to draw a visual overlay.

The overlay:

- Highlights the inspected element
- Highlights related elements
- Draws labelled arrows between them
- Uses click-through, non-activating windows
- Does not interfere with pointer interaction
- Updates when the selected tree node changes

Arrow lines and arrowheads use a one-pixel black border for contrast.

The visualisation only shows relationships exposed by the selected accessibility API. It does not parse the DOM directly.

## 21. Inspected-element highlight

The inspected element is outlined using its accessibility bounding rectangle.

The highlight:

- Is topmost
- Is click-through
- Does not receive focus
- Follows the selected tree node
- Adapts to Windows High Contrast settings
- Supports per-monitor DPI scaling

Bounding rectangles are supplied by the target accessibility provider and may occasionally be empty, approximate, or offscreen.

## 22. Focus-order recording

Focus-order recording captures actual keyboard navigation rather than statically guessing tab order.

### Start recording

1. Select **Record focus order**.
2. Move to the application under test.
3. Navigate with `Tab` and `Shift+Tab`.
4. Each actual focus change is recorded as a numbered stop.

### Include arrow-key navigation

Enable **Include arrow navigation** to record focus transitions caused by:

- Left Arrow
- Right Arrow
- Up Arrow
- Down Arrow

This is useful for composite widgets such as:

- Menus
- Tab lists
- Trees
- Tree grids
- Grids
- List boxes
- Radio groups
- Toolbars

An arrow key is only recorded when the accessible focus target changes. Arrow presses that only change a value or scroll content are ignored.

### Visual meaning

- Numbered markers show focus stops
- Gold connectors show `Tab` and `Shift+Tab` transitions
- Blue connectors show arrow-key transitions
- The current or latest stop is highlighted in green

### Stop recording

Select the recording control again or press `Escape`, depending on the current build.

Stopping leaves the recorded path visible.

### Clear the path

Select **Clear focus path** to remove all recorded stops and connectors.

### Testing recommendations

- Begin from a known starting point.
- Record one direction at a time.
- Clear the path before testing another component.
- Test forward and reverse tab order separately.
- Enable arrow recording only when entering a composite widget.
- Confirm that focus is visibly indicated by the target application.
- Compare the recorded order with the expected reading and interaction order.

## 23. Choosing displayed properties

Select **Choose properties…** to open the property visibility dialog.

The dialog supports:

- Search
- Per-property checkboxes
- Select all
- Select none
- Persistent choices between runs

Property choices apply to the visible property panels.

Hidden properties remain available in the underlying inspection model and may still be included in JSON exports, depending on the current build.

## 24. Property text wrapping

Long property names and values wrap within the grid.

Rows automatically expand vertically.

Horizontal scrolling is disabled for the property values so information is not cropped.

Do not add:

```xml
RowHeight="Auto"
```

to a WPF `DataGrid`. `RowHeight` is a numeric property and `Auto` causes a startup XAML exception. Automatic row sizing is achieved by leaving `RowHeight` unset.

## 25. Always on top

Enable **Always on top** to keep AViewer above other windows.

This is useful while inspecting another application.

The setting is saved between runs.

Disable it when it interferes with application menus, dialogs, or full-screen testing.

## 26. Windows accessibility preferences

AViewer follows Windows platform accessibility settings.

Supported behaviours include:

- High Contrast colours
- Live High Contrast changes
- Windows system foreground and background colours
- Windows system message font and size
- Per-monitor DPI scaling
- Layout rounding
- System selection colours
- Keyboard-focus colours
- Keyboard-cue and access-key visibility
- No nonessential animation

Custom overlays are redrawn when relevant system settings change.

## 27. Keyboard operation

The main interface should be usable with the keyboard.

Typical keys include:

- `Tab`: move forward through controls
- `Shift+Tab`: move backward
- `Space`: toggle checkboxes and buttons
- `Enter`: activate the focused default control
- Arrow keys: navigate trees, tabs, lists, and checkboxes
- `Escape`: stop an active inspection or recording mode where implemented
- `Alt` plus an access key: activate controls with access-key labels

Windows keyboard-cue settings determine when access-key underlines are displayed.

## 28. Copying and exporting JSON

AViewer can copy or save the current inspection data as JSON.

The exported data may include:

- Selected API
- Current tree
- Properties
- Bounding rectangles
- Relationships
- Table information
- Child nodes

Use JSON export to:

- Compare accessibility implementations
- Attach evidence to bug reports
- Record test results
- Analyse properties programmatically
- Compare UIA, MSAA, and IA2 output

Review exported data before sharing it. Accessible names, values, document content, URLs, and application text may contain sensitive information.

## 29. Settings files

Persistent settings are stored under:

```text
%LOCALAPPDATA%\AViewerModern\
```

Typical files include:

```text
app-settings.json
property-filter.json
```

The files may store:

- Always-on-top setting
- Relationship visualisation setting
- Focus-order options
- Property visibility choices
- Other interface preferences

## 30. Reset all settings

Close AViewer, then run:

```powershell
Remove-Item "$env:LOCALAPPDATA\AViewerModern\app-settings.json" `
  -Force `
  -ErrorAction SilentlyContinue

Remove-Item "$env:LOCALAPPDATA\AViewerModern\property-filter.json" `
  -Force `
  -ErrorAction SilentlyContinue
```

Start AViewer again.

## 31. Troubleshooting

### Project file not found

Error:

```text
The provided file path does not exist
```

Locate the project:

```powershell
Get-ChildItem -Recurse -Filter AViewer.App.csproj
```

Change into the correct project root or use the full path returned.

### Duplicate MainWindow definitions

Errors may mention:

```text
IComponentConnector.Connect
InitializeComponent
MainWindow already contains a definition
```

Cause:

Two XAML files declare:

```text
AViewer.App.MainWindow
```

Remove renamed copies such as:

```text
MainWindow-wrap.xaml
MainWindow-api-tabs.xaml
MainWindow-focus-order.xaml
```

Keep only:

```text
MainWindow.xaml
MainWindow.xaml.cs
```

Then remove `bin` and `obj`.

### XAML root-level data error

Error:

```text
Data at the root level is invalid
```

The XAML file may contain:

- Markdown code fences
- Explanatory text
- An invalid byte-order mark
- Content before the opening `<Window>` element

The first meaningful characters must be:

```xml
<Window
```

### `Auto` is not valid for `Double`

Error:

```text
Auto is not a valid value for Double
```

Remove:

```xml
RowHeight="Auto"
```

from the `DataGrid`.

### Missing `Path`, `File`, `Directory`, or `IOException`

Add:

```csharp
using System.IO;
```

to the affected C# file.

### Unsafe code required

Add this to the application project’s main `PropertyGroup`:

```xml
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
```

This is required for source-generated Win32 interop using `LibraryImport`.

### Application opens and immediately closes

Temporarily change:

```xml
<OutputType>WinExe</OutputType>
```

to:

```xml
<OutputType>Exe</OutputType>
```

Run from PowerShell to expose the exception.

Restore `WinExe` after fixing the error.

### Application builds but does not inspect elevated applications

Run AViewer as Administrator.

### IA2 reports unavailable

Test with a known IA2 provider such as:

- Chrome
- Edge
- Firefox

Ensure browser accessibility is active and inspect actual document content rather than only browser chrome.

The IA2 acquisition path uses:

1. Direct `QueryInterface`
2. `IServiceProvider`
3. `QueryService(IID_IAccessible, IID_IAccessible2)`

### IA2 is available but has limited properties

Possible reasons:

- The target does not implement the requested interface
- The selected object is an MSAA object without richer IA2 data
- The browser has not fully initialised accessibility
- The property returns `S_FALSE` or `E_NOTIMPL`
- The inspected point resolves to a container rather than the intended child

Pause over the element, increase tree depth, and select the relevant IA2 child node.

### Role appears as MSAA rather than IA2

IA2 `role()` may return either:

- An IA2-specific role, or
- A standard MSAA role

AViewer should display both as readable role names.

### Relationship arrows do not appear

Check:

- **Show relationships** is enabled
- The selected node exposes relationships
- Related elements have valid bounding rectangles
- The selected API exposes the relationship
- The related element is on screen
- The relationship is not an intentionally excluded containment relation

### Focus-order stops do not record

Check:

- Recording is active
- Focus moved to a different accessible element
- AViewer is not the focused application
- The target exposes focus changes through the accessibility API
- Arrow-key recording is enabled when testing composite widgets
- The key did not only change a value or scroll content

### Build behaves inconsistently after replacing files

Delete generated outputs:

```powershell
Remove-Item -Recurse -Force .\src\AViewer.App\obj `
  -ErrorAction SilentlyContinue

Remove-Item -Recurse -Force .\src\AViewer.App\bin `
  -ErrorAction SilentlyContinue

Remove-Item -Recurse -Force .\src\AViewer.Core\obj `
  -ErrorAction SilentlyContinue

Remove-Item -Recurse -Force .\src\AViewer.Core\bin `
  -ErrorAction SilentlyContinue

dotnet clean
dotnet restore
dotnet build
```

## 32. Recommended test workflow

1. Start the target application.
2. Start AViewer at the same integrity level.
3. Enable **Always on top** if useful.
4. Choose the API tab you want to evaluate.
5. Set a modest tree depth.
6. Start pointer or focus inspection.
7. Select the precise tree node.
8. Review role, name, state, value, and relationships.
9. Review table properties where relevant.
10. Enable relationship visualisation.
11. Record keyboard focus order separately.
12. Export JSON when evidence is needed.
13. Repeat using the other API tabs.
14. Compare differences between UIA, MSAA, and IA2.

## 33. Browser testing workflow

For Chrome, Edge, or Firefox:

1. Open a simple test page.
2. Start AViewer.
3. Select the IA2 tab.
4. Start pointer inspection.
5. Move over document content.
6. Select the matching IA2 tree node.
7. Confirm:
   - Name
   - Role
   - States
   - Attributes
   - Relationships
   - Table information, where applicable
8. Switch to UIA and compare output.
9. Record tab order.
10. Enable arrow recording when testing composite widgets.

Browser chrome and web content may use different accessibility implementations.

## 34. Table testing workflow

1. Inspect the table element.
2. Review UIA table or grid properties.
3. Inspect an individual cell.
4. Review row, column, spans, and containing grid.
5. Review row and column header items.
6. Switch to IA2.
7. Review `IAccessibleTable2` information.
8. Inspect an IA2 table cell.
9. Review row/column coordinates and header-cell relations.
10. Enable relationship visualisation to display header associations.

## 35. Relationship testing workflow

For `aria-labelledby`:

1. Inspect the labelled control.
2. Open UIA or IA2.
3. Look for **Labeled by**.
4. Enable relationship visualisation.
5. Confirm the arrow points to the labelling element.

For `aria-describedby`:

1. Inspect the described control.
2. Look for **Described by**.
3. Confirm the relationship target and accessible description.

For HTML tables:

1. Inspect a data cell.
2. Review row and column headers.
3. Enable relationship visualisation.
4. Confirm arrows connect the cell to the expected header cells.

## 36. Focus-order testing workflow

1. Clear the existing focus path.
2. Start focus-order recording.
3. Move focus to a known starting control.
4. Press `Tab` repeatedly.
5. Review the numbered sequence.
6. Test `Shift+Tab` separately.
7. Enter a composite widget.
8. Enable arrow navigation recording.
9. Navigate the widget with arrow keys.
10. Confirm blue connectors match the widget’s expected internal navigation.
11. Check for:
    - Missing stops
    - Duplicate stops
    - Unexpected jumps
    - Focus traps
    - Offscreen focus
    - Focus moving into hidden content
    - Illogical reverse order
12. Save evidence if needed.

## 37. Known limitations

- Accessibility information depends on the target application’s provider.
- UIA, MSAA, and IA2 trees may not map one-to-one.
- Browser providers may initialise accessibility lazily.
- Some properties return `S_FALSE`, `E_NOTIMPL`, or empty values.
- Some elements expose invalid or empty bounding rectangles.
- Relationship visualisation uses accessibility relations, not direct DOM inspection.
- Focus-order recording captures observed navigation; it does not statically calculate every possible route.
- Arrow-key recording cannot infer semantic intent when focus does not move.
- Very large trees may be slow.
- Inspecting elevated applications requires an elevated AViewer process.
- Cross-desktop, secure-desktop, and some protected application surfaces may not be inspectable.
- The project is Windows-only.

## 38. Returning to a normal windowed build

If you changed the project to use console output for debugging, restore:

```xml
<OutputType>WinExe</OutputType>
```

Then clean and publish again.

## 39. Updating the project safely

When receiving a new project ZIP:

1. Extract it into a new empty directory.
2. Do not merge it with an old source tree.
3. Restore packages.
4. Build before copying settings.
5. Run the new build.
6. Delete or archive the old build only after verification.

This avoids duplicate XAML classes and stale generated files.
