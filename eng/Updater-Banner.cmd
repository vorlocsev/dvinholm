@echo off
setlocal
rem Standalone Windows 11 updater. Only built-in Windows PowerShell 5.1 is used.
set "DvinholmPs=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if not exist "%DvinholmPs%" (
 echo ERROR: Windows PowerShell 5.1 is not available.
 pause
 exit /b 10
)
if not exist "%~dp0Updater-Banner.ps1" (
 echo ERROR: Updater-Banner.ps1 is missing.
 pause
 exit /b 11
)
"%DvinholmPs%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Updater-Banner.ps1" -Mode UI
exit /b %ERRORLEVEL%
