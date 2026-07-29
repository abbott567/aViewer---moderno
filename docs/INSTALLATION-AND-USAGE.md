# AViewer Moderno installation and usage

## Install the development tools

Install one of the following on Windows 10 or Windows 11:

- the .NET 8 SDK; or
- Visual Studio 2022 with the **.NET desktop development** workload.

Confirm the SDK is available:

```powershell
dotnet --list-sdks
```

At least one `8.0.x` entry should be listed.

## Extract the project

Extract the ZIP into a new empty directory. Do not merge it over an older source tree, because renamed XAML files can create duplicate generated classes.

Example:

```powershell
Expand-Archive `
  "$HOME\Downloads\aviewer-moderno-full-latest.zip" `
  "E:\aviewer-moderno-full-latest" `
  -Force

Set-Location "E:\aviewer-moderno-full-latest"
```

Confirm the project exists:

```powershell
Test-Path .\src\AViewer.App\AViewer.App.csproj
```

## Build

```powershell
dotnet restore .\AViewer.sln
dotnet build .\AViewer.sln -c Debug
```

## Run

```powershell
dotnet run --project .\src\AViewer.App\AViewer.App.csproj
```

Run both AViewer and the inspected program at the same integrity level. To inspect an application running as Administrator, start AViewer from an elevated PowerShell window.

## Inspect by pointer

1. Press `F7` or activate **Pointer inspect**.
2. Move the pointer over an application outside AViewer.
3. The selected API tree and properties update.
4. Press `F7` again or `Escape` to stop.

## Inspect keyboard focus

1. Press `F8` or activate **Focus inspect**.
2. Move to the application under test.
3. Navigate with the keyboard.
4. AViewer follows the currently focused element.

## Select an accessibility API

Use the UIA, MSAA and IAccessible2 tabs. The selected tab controls both the visible tree and the property grid.

## Relationship visualisation

Enable **View > Show relationships**. Selecting a node with relationships draws:

- one border around the source;
- one border around every separate visible target;
- one clearly defined arrow from the source border to each target border.

No relationship labels are drawn. Self-references are ignored. Hidden targets with no truthful screen rectangle cannot be outlined. Missing provider rectangles are matched against the current accessibility snapshots when possible.

The multi-target case at `https://cdpn.io/pen/debug/VwyrxQJ` should outline each visible separate `aria-labelledby` target and draw an arrow to it.

For table cells, AViewer reads row-header and column-header associations from both UIA `TableItemPattern` and IA2 `IAccessibleTableCell`. These associations appear first in the property panel under **Relationships** and are also passed to the visualisation. Open `docs\relationship-test-page.html` in a browser to verify a cell with both row and column headers.

## Focus-order recording

1. Press `F9` or activate **Focus order**.
2. Move to the target application.
3. Navigate with `Tab` and `Shift+Tab`.
4. Enable **Include arrow-key navigation** when testing composite widgets.
5. Press `F9` or `Escape` to stop.

Each focus stop is numbered. Number badges are placed outside the element border so they do not cover the focused control. The overlay does not add key-name labels and uses the same line and arrowhead style as relationships.

## View and navigate the complete accessibility tree

1. Inspect an element in the target application or web page.
2. Press `Ctrl+Shift+A`, activate **Complete tree**, or use **Navigate > Load complete app or page tree**.
3. For web content, AViewer loads the enclosing document when exposed by the API. For native software, it loads the application accessibility root.
4. Use **Navigate > Expand complete tree** or **Collapse tree** to control the tree presentation.
5. Select any node to inspect its properties and relationships.

The tree-depth selector includes **All**. Complete loading is intentionally user initiated because large application and browser trees can contain thousands of nodes.

## Move up the accessibility tree

Select a node and press `Alt+Up`, or use **Navigate > Up one level**.

## Copy and export

The File menu can:

- copy the selected element as approximate HTML;
- copy its loaded subtree as approximate HTML;
- copy the active accessibility tree as JSON;
- save the active accessibility tree as JSON.

Review exports before sharing them because accessible names, values and application text may contain sensitive information.

## Property preferences

Use **View > Preferences**. The dialog lists the complete UIA, MSAA and IAccessible2 property catalogue, not only properties found in the current snapshot. Use **Select all API properties** to display every property returned by the active API. Enable **Show selected properties when the API does not expose a value** to show missing values as **Not exposed**.

Choices are stored in:

```text
%LOCALAPPDATA%\AViewerModern\property-filter.json
```

Application settings are stored in:

```text
%LOCALAPPDATA%\AViewerModern\app-settings.json
```

Delete either file while AViewer is closed to reset that group of settings.

Relationship properties are included in the same preference list and are selected by default.

## Add Help-menu links

Edit `src\AViewer.App\HelpMenuLinks.json`. It is copied beside the executable during build. Only absolute HTTP and HTTPS addresses are opened.

## Publish

```powershell
dotnet publish .\src\AViewer.App\AViewer.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true
```

## Troubleshooting

### The application cannot inspect an elevated program

Run AViewer as Administrator too.

### IA2 is unavailable

Test document content in a current Chromium browser or Firefox. Browser chrome and document content can expose different accessibility APIs.

### Duplicate class or `InitializeComponent` errors

Extract the project into an empty directory. Keep only one XAML file for each `x:Class`.

### Build output is stale

```powershell
Get-ChildItem . -Recurse -Directory -Include bin,obj | Remove-Item -Recurse -Force
dotnet build .\AViewer.sln
```

### Relationship source border appears without targets

This version intentionally hides a relationship overlay unless at least one separate drawable target exists. Confirm that you built this complete archive rather than copying only older overlay files.
