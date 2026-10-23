@echo off
setlocal
echo Dvinholm Lite prerequisite inspection
echo No software will be installed or changed.
echo.
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%~dp0Check-Lite-Prerequisites.ps1" -LiteDirectory "%~dp0" -WindowsAppSdkLine "2.5"
set "DvinholmLiteExitCode=%ERRORLEVEL%"
echo.
if not "%DvinholmLiteExitCode%"=="0" echo Lite prerequisites are missing or could not be verified.
pause
exit /b %DvinholmLiteExitCode%
