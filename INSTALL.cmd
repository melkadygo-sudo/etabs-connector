@echo off
setlocal
title StructLink for ETABS Setup
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\setup.ps1"
set "RESULT=%ERRORLEVEL%"
echo.
pause
exit /b %RESULT%
