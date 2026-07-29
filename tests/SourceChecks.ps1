$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$app = Join-Path $root 'src\AViewer.App'
$core = Join-Path $root 'src\AViewer.Core'

$required = @(
    'AViewer.sln',
    'src\AViewer.App\AViewer.App.csproj',
    'src\AViewer.App\MainWindow.xaml',
    'src\AViewer.App\MainWindow.xaml.cs',
    'src\AViewer.App\PropertySelectionWindow.xaml.cs',
    'src\AViewer.App\AppSettingsService.cs',
    'src\AViewer.App\HelpMenuLinkService.cs',
    'src\AViewer.App\PropertyFilterService.cs',
    'src\AViewer.App\ApiPropertyCatalog.cs',
    'src\AViewer.App\RelationshipOverlayWindow.xaml.cs',
    'src\AViewer.App\FocusOrderOverlayWindow.xaml.cs',
    'src\AViewer.App\OverlayArrowRenderer.cs',
    'src\AViewer.App\RelationshipTargetBoundsResolver.cs',
    'src\AViewer.Core\Services\Ia2Inspector.cs',
    'src\AViewer.Core\Services\Uia3Inspector.cs'
)

foreach ($file in $required) {
    if (-not (Test-Path (Join-Path $root $file))) {
        throw "Missing required file: $file"
    }
}

$relationship = Get-Content (Join-Path $app 'RelationshipOverlayWindow.xaml.cs') -Raw
$focus = Get-Content (Join-Path $app 'FocusOrderOverlayWindow.xaml.cs') -Raw
$main = Get-Content (Join-Path $app 'MainWindow.xaml.cs') -Raw
$selection = Get-Content (Join-Path $app 'PropertySelectionWindow.xaml.cs') -Raw
$ia2 = Get-Content (Join-Path $core 'Services\Ia2Inspector.cs') -Raw
$uia = Get-Content (Join-Path $core 'Services\Uia3Inspector.cs') -Raw
$coreProject = Get-Content (Join-Path $core 'AViewer.Core.csproj') -Raw
$appProject = Get-Content (Join-Path $app 'AViewer.App.csproj') -Raw
$mainXaml = Get-Content (Join-Path $app 'MainWindow.xaml') -Raw

if (($uia | Select-String -Pattern 'new System\.Windows\.Point\(' -AllMatches).Matches.Count -lt 2 -or
    $uia -notmatch 'System\.Windows\.Rect\.Empty') {
    throw 'Uia3Inspector does not explicitly resolve WPF Point and Rect types.'
}
if ($coreProject -match '<UseWindowsForms>true</UseWindowsForms>' -or
    $coreProject -match '<Reference Include="UIAutomation(?:Client|Types)"') {
    throw 'The core project contains obsolete Windows Forms or direct UI Automation references.'
}
if ($coreProject -notmatch '<UseWPF>true</UseWPF>') {
    throw 'The core project is missing its WPF Windows Desktop framework reference.'
}

$ioFiles = @('AppSettingsService.cs', 'HelpMenuLinkService.cs', 'PropertyFilterService.cs')
foreach ($name in $ioFiles) {
    $content = Get-Content (Join-Path $app $name) -Raw
    if ($content -notmatch 'using System\.IO;') {
        throw "$name uses file-system APIs without importing System.IO."
    }
}

if ($selection -notmatch 'IReadOnlyList<PropertyChoice>\s+AllChoices\s*=>\s*_all\s*;' -or
    $selection -notmatch 'ShowUnavailableProperties' -or
    $main -notmatch '_propertyFilter\.Apply\(dialog\.AllChoices\)' -or
    $main -notmatch 'ApiPropertyCatalog\.All') {
    throw 'Preferences do not expose the complete API property catalogue.'
}

$sourceFiles = Get-ChildItem (Join-Path $root 'src') -Filter '*.cs' -File -Recurse
foreach ($sourceFile in $sourceFiles) {
    $content = Get-Content $sourceFile.FullName -Raw
    if ($content -match '\{\s*get;(?:\s*set;)?\s*\};') {
        throw "Malformed semicolon after property accessor block in $($sourceFile.FullName)."
    }
}

$typeLocations = @{}
foreach ($sourceFile in $sourceFiles) {
    $content = Get-Content $sourceFile.FullName -Raw
    $namespaceMatch = [regex]::Match($content, '(?m)^namespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*;')
    $namespace = if ($namespaceMatch.Success) { $namespaceMatch.Groups[1].Value } else { '' }
    $matches = [regex]::Matches(
        $content,
        '(?m)^(?:public|internal|private|protected)?\s*(?:sealed\s+|static\s+|partial\s+|abstract\s+)*(?:class|record|struct|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)')
    foreach ($match in $matches) {
        $key = "$namespace.$($match.Groups[1].Value)"
        if ($typeLocations.ContainsKey($key)) {
            throw "Duplicate type declaration: $key in $($typeLocations[$key]) and $($sourceFile.FullName)."
        }
        $typeLocations[$key] = $sourceFile.FullName
    }
}

if ($appProject -notmatch '<Compile Remove="AccessibilityVisualPalette\.cs"\s*/>' -or
    $appProject -notmatch '<Compile Remove="HelpMenuLink\.cs"\s*/>') {
    throw 'The app project does not exclude known obsolete source filenames.'
}

if ($mainXaml -notmatch 'x:Name="MainMenu"' -or
    $mainXaml -notmatch 'Header="_File"' -or
    $mainXaml -notmatch 'Header="_Inspect"' -or
    $mainXaml -notmatch 'Header="_View"' -or
    $mainXaml -notmatch 'Header="_Navigate"' -or
    $mainXaml -notmatch 'x:Name="HelpMenu"') {
    throw 'The persistent application menu is incomplete or missing.'
}
if ($mainXaml -notmatch 'MinHeight="28"' -or
    $mainXaml -notmatch 'AutomationProperties.Name="Application menu"') {
    throw 'The application menu does not have the persistent visible layout safeguards.'
}

if ($relationship -match 'TextBlock|CreateLabel|Draw\w*Label') {
    throw 'Relationship overlay contains label-rendering code.'
}
if ($focus -notmatch 'DrawStopNumber' -or
    $focus -notmatch 'stopNumber\.ToString\(\)' -or
    $focus -notmatch 'rect\.Top\s*-\s*minimumSize') {
    throw 'Focus-order stops are not numbered outside element bounds.'
}
if ($relationship -notmatch 'RelationshipTargetBrush' -or $relationship -notmatch 'DrawRectangle') {
    throw 'Relationship target borders are not present.'
}
if ($main -notmatch 'RelationshipTargetBoundsResolver\.Resolve' -or
    $main -notmatch 'Relationships\s*=\s*resolvedRelationships') {
    throw 'Target-bound recovery is not fully integrated into MainWindow.'
}
if ($relationship -notmatch 'OverlayArrowRenderer\.DrawArrow' -or
    $focus -notmatch 'OverlayArrowRenderer\.DrawArrow') {
    throw 'The overlays are not using the shared arrow renderer.'
}
if ($relationship -notmatch 'TryBuildOrthogonalRoute' -or
    $focus -notmatch 'TryBuildOrthogonalRoute') {
    throw 'The overlays are not using side-aware orthogonal route construction.'
}
$arrowRenderer = Get-Content (Join-Path $app 'OverlayArrowRenderer.cs') -Raw
if ($arrowRenderer -notmatch 'targetRect\.Top\s*>=\s*sourceRect\.Bottom' -or
    $arrowRenderer -notmatch 'targetRect\.Bottom\s*<=\s*sourceRect\.Top' -or
    $arrowRenderer -notmatch 'targetRect\.Left\s*>=\s*sourceRect\.Right' -or
    $arrowRenderer -notmatch 'targetRect\.Right\s*<=\s*sourceRect\.Left' -or
    $arrowRenderer -notmatch 'StrokeStartLineCap\s*=\s*PenLineCap\.Flat') {
    throw 'The arrow renderer does not enforce facing-border, gap-only routing.'
}
if ($main -notmatch 'BuildDisplayedProperties' -or
    $main -notmatch 'BuildRelationshipProperties' -or
    $main -notmatch '"Relationships"') {
    throw 'Relationship information is not integrated into the property table.'
}
if ($main -notmatch '_propertyFilter\.Filter\(displayed\)' -or
    $main -notmatch 'ShowUnavailableProperties' -or
    $main -notmatch '"Not exposed"') {
    throw 'Property preferences do not support displaying the complete selected API property set.'
}
if ($uia -notmatch 'TableItemPattern\.ColumnHeaderItemsProperty' -or
    $uia -notmatch 'TableItemPattern\.RowHeaderItemsProperty' -or
    $uia -notmatch 'GetColumnHeaderItems\(' -or
    $uia -notmatch 'GetRowHeaderItems\(') {
    throw 'UIA table-cell header relationships are not extracted.'
}
if ($ia2 -notmatch 'IidIAccessibleTableCell' -or
    $ia2 -notmatch 'TableCellColumnHeaderCellsSlot' -or
    $ia2 -notmatch 'TableCellRowHeaderCellsSlot' -or
    $ia2 -notmatch 'ReadTableCellHeaderTargets') {
    throw 'IA2 table-cell header relationships are not extracted.'
}
if ($ia2 -notmatch 'SameRelationship\(existing, relationship\)' -or
    $ia2 -notmatch 'SameRelationship\(existing, mapped\)') {
    throw 'IA2 relationship deduplication is not relationship-type aware.'
}
if ($mainXaml -notmatch 'x:Key="InspectSwitchStyle"' -or
    ($mainXaml | Select-String -Pattern 'Style="\{StaticResource InspectSwitchStyle\}"' -AllMatches).Matches.Count -lt 2 -or
    $mainXaml -notmatch 'Pointer inspection switch' -or
    $mainXaml -notmatch 'Keyboard focus inspection switch') {
    throw 'Pointer and focus inspection are not presented as accessible switches.'
}
if ($ia2 -notmatch 'targetIndex\s*<\s*targetCount') {
    throw 'IA2 multi-target relation enumeration was not found.'
}
if ($main -notmatch 'LoadCompleteTree_Click' -or
    $main -notmatch '_inspector\.InspectComplete' -or
    $main -notmatch 'ExpandAll_Click' -or
    $mainXaml -notmatch 'Load complete _app or page tree' -or
    $mainXaml -notmatch 'ComboBoxItem Content="All"' -or
    $uia -notmatch 'FindCompleteRoot' -or
    $ia2 -notmatch 'InspectCompleteTreesPoint') {
    throw 'Complete application or web-page accessibility-tree navigation is not fully integrated.'
}

$handlerPairs = @(
    @('MainWindow_PreviewKeyDown', $main),
    @('CopyHtml_Click', $main),
    @('CopyHtmlSubtree_Click', $main),
    @('CopyJson_Click', $main),
    @('SaveJson_Click', $main),
    @('Exit_Click', $main),
    @('InspectPointer_Click', $main),
    @('InspectFocus_Click', $main),
    @('RecordFocusOrder_Click', $main),
    @('ClearFocusPath_Click', $main),
    @('IncludeArrowNavigation_Changed', $main),
    @('ShowRelationships_Changed', $main),
    @('AlwaysOnTop_Changed', $main),
    @('ChooseProperties_Click', $main),
    @('LoadCompleteTree_Click', $main),
    @('ExpandAll_Click', $main),
    @('CollapseAll_Click', $main),
    @('UpOneLevel_Click', $main),
    @('ApiTabControl_SelectionChanged', $main),
    @('SearchBox_TextChanged', $selection),
    @('SelectAll_Click', $selection),
    @('SelectNone_Click', $selection),
    @('Ok_Click', $selection)
)
foreach ($pair in $handlerPairs) {
    if ($pair[1] -notmatch "\b$([regex]::Escape($pair[0]))\s*\(") {
        throw "Missing XAML event handler: $($pair[0])."
    }
}

Write-Host 'Source checks passed.'
