@echo off
setlocal
cd /d "%~dp0"
where pwsh.exe >nul 2>nul
if %ERRORLEVEL%==0 (
  pwsh.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0START-CRANEOPT-REV05.ps1"
) else (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0START-CRANEOPT-REV05.ps1"
)
if errorlevel 1 (
  echo.
  echo REV05 launch failed. Review the error above; do not run an older build.
  pause
  exit /b 1
)
endlocal
