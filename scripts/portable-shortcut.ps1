$ErrorActionPreference = 'Stop'
$portableExe = Join-Path $PSScriptRoot 'AiGuardLock.exe'
if (-not (Test-Path -LiteralPath $portableExe)) { throw 'AiGuardLock.exe was not found next to this script.' }
$portableDesktop = [Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $portableDesktop 'AI Guard Lock.lnk'
$shell = New-Object -ComObject WScript.Shell
if (Test-Path -LiteralPath $shortcutPath) {
    $existingShortcut = $shell.CreateShortcut($shortcutPath)
    if ($existingShortcut.TargetPath -ne $portableExe) {
        $shortcutPath = Join-Path $portableDesktop ('AI Guard Lock ' + [DateTime]::Now.ToString('yyyyMMdd-HHmmss') + '.lnk')
    }
}
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $portableExe
$shortcut.WorkingDirectory = $PSScriptRoot
$shortcut.IconLocation = "$portableExe,0"
$shortcut.Description = 'AI Guard Lock - translucent screen cover'
$shortcut.Save()
Write-Output "Created: $shortcutPath"
