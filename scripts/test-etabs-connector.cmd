@echo off
setlocal
title StructLink for ETABS Diagnostic Test
cd /d "%~dp0"

echo Starting the StructLink for ETABS diagnostic test...
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
