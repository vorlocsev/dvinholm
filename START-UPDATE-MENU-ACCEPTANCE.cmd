@echo off
setlocal DisableDelayedExpansion
rem Update-menu Windows 11 LTSC acceptance ONLY. NEVER A STABLE RELEASE.
rem Run from the extracted single-download GitHub Actions bundle.
rem Do not interpolate any external commit, download URL or user argument.
if "%~1"=="" goto launch
if /I "%~1"=="--prepare-only" if "%~2"=="" goto prepare
echo ERROR: Unsupported arguments. Use no argument or --prepare-only.
exit /b 2
:prepare
call "%~dp0kit\START-STABLE-ACCEPTANCE-024.cmd" --prepare-only
exit /b %errorlevel%
:launch
call "%~dp0kit\START-STABLE-ACCEPTANCE-024.cmd"
exit /b %errorlevel%
