# MD.converter360 - Desktop Shortcut Creator
# Run this script to create a desktop shortcut for MD.converter360

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "  ================================================" -ForegroundColor Cyan
Write-Host "       MD.converter360 - Desktop Shortcut Setup" -ForegroundColor Cyan
Write-Host "  ================================================" -ForegroundColor Cyan
Write-Host ""

# Get paths
$desktopPath = [Environment]::GetFolderPath("Desktop")
$scriptPath = "D:\AI_projects\MD.converter360\Start.bat"
$shortcutPath = Join-Path $desktopPath "MD.converter360.lnk"

# Create shortcut using WScript.Shell
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $scriptPath
$shortcut.WorkingDirectory = "D:\AI_projects\MD.converter360"
$shortcut.Description = "MD.converter360 - Document Converter | Backend:5294 Frontend:5172"
$shortcut.IconLocation = "%SystemRoot%\System32\imageres.dll,102"  # Document icon (cyan/blue)
$shortcut.Save()

Write-Host "  [OK] Desktop shortcut created!" -ForegroundColor Green
Write-Host ""
Write-Host "  Location: $shortcutPath" -ForegroundColor Gray
Write-Host ""

# Clean up old shortcut names if they exist
$oldNames = @(
    "MDConverter360.lnk",
    "Start MDConverter360.lnk",
    "MD Converter.lnk"
)

foreach ($oldName in $oldNames) {
    $oldPath = Join-Path $desktopPath $oldName
    if (Test-Path $oldPath) {
        Remove-Item $oldPath -Force
        Write-Host "  [Cleanup] Removed old shortcut: $oldName" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "  ================================================" -ForegroundColor Cyan
Write-Host "       Setup Complete!" -ForegroundColor Green
Write-Host "  ================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Double-click 'MD.converter360' on your desktop to start." -ForegroundColor White
Write-Host ""

# Pause before closing
Read-Host "  Press Enter to close"
