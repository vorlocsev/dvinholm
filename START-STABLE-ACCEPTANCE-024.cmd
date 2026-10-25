@echo off
setlocal DisableDelayedExpansion
rem Dvinholm 0.2.4 Stable staging Windows acceptance. NOT A RELEASE.
rem Run only from its unmodified, matching GitHub Actions acceptance kit.
rem No untrusted SHA or external path is ever expanded into CMD commands.
if "%~1"=="" goto launch
if /I "%~1"=="--prepare-only" if "%~2"=="" goto prepare
echo ERROR: Unsupported arguments. Use no argument or --prepare-only.
exit /b 2
:prepare
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%~dp0eng\Start-StableAcceptance024.ps1" -PrepareOnly
exit /b %errorlevel%
:launch
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%~dp0eng\Start-StableAcceptance024.ps1"
exit /b %errorlevel%
