# AViewer Moderno

AViewer Moderno is a Windows desktop accessibility inspector for:

- Microsoft UI Automation (UIA)
- Microsoft Active Accessibility (MSAA / `IAccessible`)
- IAccessible2 (IA2)

The application includes pointer inspection, keyboard-focus inspection, complete application and web-page accessibility-tree navigation, configurable API property display, JSON and HTML export, relationship visualisation and focus-order recording.

## Current visualisation behaviour

Relationship visualisation:

- outlines the source element;
- outlines every separate drawable target element;
- draws one arrow per target;
- routes targets below from the source bottom border to the target top border;
- uses the equivalent facing borders for targets above, left or right;
- keeps connector shafts and arrowheads outside source and target content;
- uses an exterior corner route where adjacent elements leave too little space for an arrowhead;
- omits relationship labels;
- removes duplicate and self-referential targets;
- recovers missing target rectangles from the current UIA, MSAA and IA2 trees where a matching visible target exists;

Focus-order visualisation:

- numbers every recorded focus stop with a badge positioned outside the element border;
- does not add key-name labels;
- outlines each focus stop;
- uses the same outlined shaft, filled arrowhead and side-aware routing as relationship visualisation;
- connects element border to element border without drawing through either focus stop.

Relationship details are also shown in the active API property table. Rows include the relationship type and source, each target's role and name, target ID, and drawable bounds. Table cells include row-header and column-header associations from UIA `TableItemPattern` and IA2 `IAccessibleTableCell`, plus row, column and span metadata when the provider exposes it.

`docs/relationship-test-page.html` contains generic ARIA relationships and table cells with explicit row and column headers for local verification in a browser.

## Requirements

- Windows 10 or Windows 11
- .NET 8 SDK, or Visual Studio 2022 with the **.NET desktop development** workload
- Matching process integrity: run AViewer as Administrator only when inspecting an elevated application

## Menu and inspect controls

The main window includes a persistent menu bar containing **File**, **Inspect**, **View**, **Navigate**, and **Help**. The menu bar is placed in its own reserved layout row and uses platform control colours so it remains visible with Windows contrast and colour settings.

Pointer inspect and Focus inspect are compact toggle switches. Their checked state, keyboard focus indication and colours follow Windows platform settings.


## Complete accessibility tree

Inspect an element in an application or web page, then use **Navigate > Load complete app or page tree** or press `Ctrl+Shift+A`. AViewer chooses the enclosing document for web content where the accessibility API exposes one; otherwise it loads the target application's accessibility root. Use **Expand complete tree** and **Collapse tree** to navigate large trees. The tree-depth selector also includes **All**.

## Property preferences

Use **View > Preferences** to choose from the complete catalogue of UIA, MSAA and IAccessible2 properties. **Select all API properties** enables every property. Enable **Show selected properties when the API does not expose a value** to retain missing selected properties in the panel with the value **Not exposed**.

## Build and run

Open PowerShell in this directory:

```powershell
dotnet restore .\AViewer.sln
dotnet build .\AViewer.sln -c Debug
dotnet run --project .\src\AViewer.App\AViewer.App.csproj
```

Or run:

```powershell
.\build.ps1
```

### Clean rebuild after replacing an earlier version

```powershell
dotnet clean .\AViewer.sln
Remove-Item .\src\AViewer.App\bin, .\src\AViewer.App\obj -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item .\src\AViewer.Core\bin, .\src\AViewer.Core\obj -Recurse -Force -ErrorAction SilentlyContinue
dotnet restore .\AViewer.sln
dotnet build .\AViewer.sln -c Debug
```

The core project uses the WPF Windows Desktop framework profile. UI Automation point and rectangle types are explicitly qualified as `System.Windows.Point` and `System.Windows.Rect`. UI Automation assemblies are supplied by the WPF framework reference rather than duplicate direct assembly references.

## Publish a standalone executable

```powershell
dotnet publish .\src\AViewer.App\AViewer.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true
```

The output is normally written to:

```text
src\AViewer.App\bin\Release\net8.0-windows\win-x64\publish\
```

## Keyboard commands

- `F7`: toggle pointer inspection
- `F8`: toggle keyboard-focus inspection
- `F9`: toggle focus-order recording
- `Alt+Up`: inspect the selected element's parent
- `Ctrl+Shift+A`: load the complete accessibility tree for the selected application or web page
- `Escape`: stop the active inspection or focus-order recording mode
- `Ctrl+C`: copy the active accessibility tree as JSON
- `Ctrl+S`: save the active accessibility tree as JSON

## Project structure

```text
AViewer.sln
src/
  AViewer.Core/
    Models/
    Services/
  AViewer.App/
    MainWindow.xaml
    relationship and focus overlay windows
    settings, export and property-filter UI
docs/
```

See `docs/INSTALLATION-AND-USAGE.md` for fuller instructions and troubleshooting.

## Build correction (29 July 2026)

`Uia3Inspector.cs` now uses fully qualified `System.Windows.Point` and `System.Windows.Rect` types. The unused `UseWindowsForms` project setting has been removed from `AViewer.Core`, preventing `System.Drawing.Point` from entering the implicit using set.

## Build correction 2026.07.29.5

- Added explicit `System.IO` imports to the settings, Help-menu-link and property-filter services.
- Exposed `PropertySelectionWindow.AllChoices` so filtered search results do not discard unlisted property selections.
- Expanded the source checks and made `build.ps1` run them before restoring and compiling.

