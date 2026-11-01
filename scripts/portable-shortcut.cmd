@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Create-Desktop-Shortcut.ps1"
if errorlevel 1 (
  echo Could not create shortcut. You can create one from AiGuardLock.exe manually.
  pause
  exit /b 1
)
echo Desktop shortcut created.
pause
