@echo off
setlocal EnableDelayedExpansion
REM ============================================================
REM Intranet one-click setup
REM 1) Put 01/02/03 zip files in THIS folder (or parent)
REM 2) Double-click this bat
REM ============================================================
cd /d "%~dp0"

set "TRANSFER=%CD%"
if not exist "%TRANSFER%\01-PocTrace-Source.zip" if exist "%~dp0..\01-PocTrace-Source.zip" set "TRANSFER=%~dp0.."
if not exist "%TRANSFER%\01-PocTrace-Source.zip" if exist "%CD%\..\transfer\01-PocTrace-Source.zip" set "TRANSFER=%CD%\..\transfer"

echo TransferDir: %TRANSFER%
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup-IntranetDev.ps1" -TransferDir "%TRANSFER%"
if errorlevel 1 (
  echo.
  echo FAILED. See AIRGAP_TRANSFER_GUIDE.md
  pause
  exit /b 1
)
echo.
pause
