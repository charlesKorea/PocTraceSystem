@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-JcsUiAssets.ps1"
if errorlevel 1 (
  echo.
  echo FAILED. See Jcs.UiAssets-INSTALL.txt
  pause
  exit /b 1
)
echo.
pause
