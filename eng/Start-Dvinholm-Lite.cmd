@echo off
setlocal
rem Dvinholm Lite x86: this is the startup entrypoint even if WinUI/.NET 10
rem is missing. The dependency manager runs on Windows PowerShell 5.1.
if not exist "%~dp0Dvinholm.App.exe" (
    echo ERROR: Dvinholm.App.exe is missing.
    pause
    exit /b 10
)
if not exist "%~dp0Check-Lite-Prerequisites.ps1" (
    echo ERROR: Lite prerequisite inspector is missing.
    pause
    exit /b 11
)
if not exist "%~dp0Manage-Lite-Dependencies.ps1" (
    echo ERROR: Lite dependency manager is missing.
    pause
    exit /b 12
)
set "ps=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if not exist "%ps%" (
    echo ERROR: Windows PowerShell 5.1 was not found.
    pause
    exit /b 13
)
"%ps%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Manage-Lite-Dependencies.ps1" -Mode Launch -LiteDirectory "%~dp0."
exit /b %ERRORLEVEL%
