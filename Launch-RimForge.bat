@echo off
title RimForge

cd /d "%~dp0"

echo ==========================================
echo        RimForge
echo ==========================================
echo.

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Audit.ps1"

echo.
echo ==========================================
echo Press any key to exit...
pause >nul