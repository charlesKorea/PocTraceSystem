@echo off
setlocal
REM Pack transfer ZIPs on the online laptop
cd /d "%~dp0..\.."
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Pack-AirgapTransfer.ps1" %*
if errorlevel 1 (
  echo Pack FAILED
  pause
  exit /b 1
)
echo.
echo Open output under C:\OfflineDev\transfer\
pause
