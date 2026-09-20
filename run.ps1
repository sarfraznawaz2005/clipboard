# Cleans and runs the app in dev mode (Debug build via `dotnet run`).
# Debug builds never register for Windows startup on their own - see the #if !DEBUG guard
# in App.xaml.cs. Only an explicit Save in Settings applies that registry change.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'src\Clipboard\Clipboard.csproj'

# The app is single-instance and closing its window only hides it to the tray - so a leftover
# process from a previous run would otherwise just get woken up instead of the freshly built
# code ever starting, making rebuilt changes silently never take effect.
$existing = Get-Process -Name 'Clipboard' -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host 'Stopping previous running instance...' -ForegroundColor Cyan
    $existing | Stop-Process -Force
    Start-Sleep -Milliseconds 300
}

Write-Host 'Cleaning previous build output...' -ForegroundColor Cyan
foreach ($dir in @((Join-Path $root 'src\Clipboard\bin'), (Join-Path $root 'src\Clipboard\obj'))) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
}

Write-Host 'Running in dev mode...' -ForegroundColor Cyan
dotnet run --project $project -c Debug

exit $LASTEXITCODE
