@echo off
setlocal
echo Dvinholm Lite prerequisite inspection
echo No software will be installed or changed.
echo.

rem The directory argument ends in dot, not backslash: Windows native quoting
rem otherwise may escape the closing quote and lose later parameters.
rem Use the Windows-owned interpreter, never an executable from PATH or
rem the current working directory. Verify the extracted app is complete.
set "DvinholmLiteExitCode=0"
if not exist "%~dp0Dvinholm.App.exe" (
    echo ERROR: Dvinholm.App.exe is missing from the Lite directory.
    set "DvinholmLiteExitCode=10"
    goto :finish
)
if not exist "%~dp0Dvinholm.App.runtimeconfig.json" (
    echo ERROR: Dvinholm.App.runtimeconfig.json is missing from the Lite directory.
    set "DvinholmLiteExitCode=11"
    goto :finish
)
if not exist "%~dp0Check-Lite-Prerequisites.ps1" (
    echo ERROR: Check-Lite-Prerequisites.ps1 is missing from the Lite directory.
    set "DvinholmLiteExitCode=12"
    goto :finish
)
set "DvinholmPowerShell=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if not exist "%DvinholmPowerShell%" (
    echo ERROR: Windows PowerShell 5.1 was not found in the Windows system directory.
    set "DvinholmLiteExitCode=13"
    goto :finish
)
"%DvinholmPowerShell%" -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%~dp0Check-Lite-Prerequisites.ps1" -LiteDirectory "%~dp0." -WindowsAppSdkLine "2.5"
set "DvinholmLiteExitCode=%ERRORLEVEL%"

:finish
echo.
if not "%DvinholmLiteExitCode%"=="0" echo Lite prerequisites are missing or could not be verified.
rem CI and scripted use can disable the prompt without changing error propagation.
if /I not "%DVINHOLM_LITE_NO_PAUSE%"=="1" pause
exit /b %DvinholmLiteExitCode%
