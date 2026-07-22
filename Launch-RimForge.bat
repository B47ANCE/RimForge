@echo off
setlocal
set "APP=%~dp0src\RimForge.App\bin\Debug\net10.0-windows\RimForge.exe"
if not exist "%APP%" (
  echo RimForge has not been built. Run Build-Test-All.ps1 first.
  exit /b 1
)
start "RimForge" "%APP%" --logging
