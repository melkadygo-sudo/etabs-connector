@echo off
setlocal
title ETABS Connector Diagnostic Test
cd /d "%~dp0"

echo Starting the ETABS Connector diagnostic test...
echo.
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0test-etabs-connector.ps1"
set "RESULT=%ERRORLEVEL%"

echo.
if "%RESULT%"=="0" (
    echo Diagnostic test finished successfully.
) else (
    echo Diagnostic test found a problem. Exit code %RESULT%.
)
echo.
pause
exit /b %RESULT%
