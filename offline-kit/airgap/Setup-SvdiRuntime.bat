@echo off
setlocal
REM ============================================================
REM SVDI one-click runtime deploy
REM Place 04-PocTrace-Runtime.zip next to this bat (or parent)
REM Default shared root: X:\jHOOK
REM ============================================================
cd /d "%~dp0"

set "RUNTIME=%~dp004-PocTrace-Runtime.zip"
if not exist "%RUNTIME%" set "RUNTIME=%~dp0..\04-PocTrace-Runtime.zip"
if not exist "%RUNTIME%" set "RUNTIME="

if defined RUNTIME (
  echo RuntimeZip: %RUNTIME%
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup-SvdiRuntime.ps1" -RuntimeZip "%RUNTIME%" -SharedRoot "X:\jHOOK"
) else (
  echo No 04-PocTrace-Runtime.zip found.
  echo Creating folders only. To deploy bins later:
  echo   Setup-SvdiRuntime.ps1 -AdminBin ... -ViewerBin ...
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup-SvdiRuntime.ps1" -SharedRoot "X:\jHOOK"
)

if errorlevel 1 (
  echo.
  echo FAILED. Check X: drive mapping and ACL.
  pause
  exit /b 1
)
echo.
echo Edit connectionStrings in Admin exe.config then run Admin once.
pause
