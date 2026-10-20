@echo off
setlocal
title StructLink for ETABS Installation
cd /d "%~dp0"

echo Starting the StructLink for ETABS installer...
echo.
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-etabs-connector.ps1" -EnableWrites -EnableAnalysis
set "RESULT=%ERRORLEVEL%"

echo.
if "%RESULT%"=="0" (
    echo Installer finished successfully.
) else (
    echo Installer failed with exit code %RESULT%.
)
echo.
pause
exit /b %RESULT%
