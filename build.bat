@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
set EXITCODE=%ERRORLEVEL%
echo.
if %EXITCODE% NEQ 0 (
    echo Build failed. Exit code %EXITCODE%.
) else (
    echo Build finished.
)

exit /b %EXITCODE%
