param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$Run
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $root
try {
    # These files belonged to earlier packages. The current project uses renamed
    # internal types, so the obsolete files must not participate in compilation.
    $legacyFiles = @(
        '.\src\AViewer.App\AccessibilityVisualPalette.cs',
        '.\src\AViewer.App\HelpMenuLink.cs'
    )

    foreach ($legacyFile in $legacyFiles) {
        if (Test-Path $legacyFile) {
            Remove-Item $legacyFile -Force
        }
    }

    Get-ChildItem -Path . -Directory -Recurse |
        Where-Object Name -in 'bin', 'obj' |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

    & .\tests\SourceChecks.ps1

    dotnet restore .\AViewer.sln
    dotnet build .\AViewer.sln -c $Configuration
    if ($Run) {
        dotnet run --project .\src\AViewer.App\AViewer.App.csproj -c $Configuration
    }
}
finally {
    Pop-Location
}
