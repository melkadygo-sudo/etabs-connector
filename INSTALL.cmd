@echo off
setlocal
title ETABS Connector Setup
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\setup.ps1"
set "RESULT=%ERRORLEVEL%"
echo.
pause
exit /b %RESULT%
