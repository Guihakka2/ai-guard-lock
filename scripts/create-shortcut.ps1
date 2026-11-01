$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$appPath = Join-Path $projectRoot 'src\AiGuardLock\bin\Release\net10.0-windows10.0.22000.0\AiGuardLock.exe'
if (-not (Test-Path -LiteralPath $appPath)) { throw 'Build the app first: scripts/dev.ps1 Build' }
$shell = New-Object -ComObject WScript.Shell
$shortcutPath = Join-Path $projectRoot 'AI Guard Lock.lnk'
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $appPath
$shortcut.WorkingDirectory = $projectRoot
$shortcut.IconLocation = "$appPath,0"
$shortcut.Description = 'Translucent screen cover with password unlock'
$shortcut.Save()
Write-Output $shortcutPath
