@echo off
setlocal DisableDelayedExpansion
rem Dvinholm 0.2.3 offline acceptance, not a release or installer.
rem Extract the two workflow artifacts as sibling folders.
set "KIT=%~dp0"
set "PROVENANCE=%KIT%BUILD_COMMIT.txt"
if not exist "%PROVENANCE%" (
  echo ERROR: BUILD_COMMIT.txt missing. Download the complete kit.
  exit /b 1
)
set "EXPECTED_COMMIT="
set /p EXPECTED_COMMIT=<"%PROVENANCE%"
set "CANDIDATE=%KIT%..\Dvinholm-v0.2.3-STAGED-NOT-PUBLISHED"
if not exist "%CANDIDATE%\SHA256SUMS.txt" (
  set "CANDIDATE=%KIT%..\candidate"
)
if not exist "%CANDIDATE%\SHA256SUMS.txt" (
  echo ERROR: Extract Dvinholm-v0.2.3-STAGED-NOT-PUBLISHED beside this kit
  echo as a sibling folder named candidate, preserving its INNER ZIP.
  exit /b 1
)
set "OUTPUT=%KIT%..\Dvinholm-023-acceptance"
if exist "%OUTPUT%" (
  echo ERROR: Dvinholm-023-acceptance already exists; never overwrite an installation.
  exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%KIT%eng\Prepare-StableAcceptance023.ps1" -ArchiveDirectory "%CANDIDATE%" -ExpectedCommit "%EXPECTED_COMMIT%" -DestinationDirectory "%OUTPUT%"
if errorlevel 1 (
  echo ERROR: Exact-commit verification or extraction failed. Do not launch the app.
  exit /b 1
)
if /I "%~1"=="--prepare-only" (
  echo PASS: verified offline extraction only; no GUI was launched.
  exit /b 0
)
rem The app also detects ACCEPTANCE-0.2.3.txt on direct EXE restart.
rem Never let its settings, profiles or CAN speed write to installed v0.2.2.
set "DVINHOLM_ACCEPTANCE_DATA_DIR=%OUTPUT%\acceptance-user-data"
if not exist "%DVINHOLM_ACCEPTANCE_DATA_DIR%" mkdir "%DVINHOLM_ACCEPTANCE_DATA_DIR%"
if errorlevel 1 (
  echo ERROR: Cannot create separate acceptance settings folder. Refusing startup.
  exit /b 1
)
echo Verified 0.2.3 Portable ZIP. Attempting to open ONLY the separate test copy.
echo A launched process is NOT proof of working WinUI. Inspect the window yourself.
pushd "%OUTPUT%"
if errorlevel 1 exit /b 1
start "" "Dvinholm.App.exe"
if errorlevel 1 (
  popd
  echo ERROR: Test app launch command failed. Leave GUI acceptance pending.
  exit /b 1
)
start "" notepad.exe "ACCEPTANCE-0.2.3.txt"
popd
echo Installed Dvinholm was not replaced. Stable 0.2.3 was NOT published.
pause
exit /b 0
