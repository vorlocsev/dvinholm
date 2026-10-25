@echo off
setlocal DisableDelayedExpansion
rem Dvinholm 0.2.4 RC: verify first, then launch ONLY an isolated test copy.
rem Two downloaded GitHub Actions artifacts must come from the SAME RC run.
set "KIT=%~dp0"
set "PROVENANCE=%KIT%BUILD_COMMIT_024.txt"
if not exist "%PROVENANCE%" (
  echo ERROR: Missing BUILD_COMMIT_024.txt. Download the complete acceptance kit.
  exit /b 1
)
rem Validate the entire receipt as DATA before ever expanding its contents
rem into a CMD command line. Double quotes and shell metacharacters in a
rem tampered receipt must never become syntax during PowerShell invocation.
rem Exactly 40 lowercase hex digits with an optional final CRLF/LF only.
powershell.exe -NoProfile -NonInteractive -Command "$sha=[IO.File]::ReadAllText($env:PROVENANCE); if ($sha -cnotmatch '\A[0-9a-f]{40}(\r?\n)?\z') { exit 1 }"
if errorlevel 1 (
  echo ERROR: Invalid exact lowercase 40-character build commit. Refusing preparation.
  exit /b 1
)
set "EXPECTED_COMMIT="
set /p EXPECTED_COMMIT=<"%PROVENANCE%"
if not defined EXPECTED_COMMIT (
  echo ERROR: Empty candidate commit. Refusing preparation.
  exit /b 1
)
set "CANDIDATE=%KIT%..\Dvinholm-v0.2.4-RC-TEST-ONLY"
if not exist "%CANDIDATE%\SHA256SUMS.txt" (
  set "CANDIDATE=%KIT%..\candidate"
)
if not exist "%CANDIDATE%\Dvinholm-Windows-x86.zip" (
  echo ERROR: Extract the RC-TEST-ONLY artifact beside the kit as candidate.
  exit /b 1
)
if not exist "%CANDIDATE%\SHA256SUMS.txt" (
  echo ERROR: Missing candidate SHA256SUMS.txt. No file was created.
  exit /b 1
)
if not "%~1"=="" if /I not "%~1"=="--prepare-only" (
  echo ERROR: Unknown argument. Only --prepare-only is supported.
  exit /b 1
)
set "OUTPUT=%KIT%..\Dvinholm-024-acceptance"
if exist "%OUTPUT%" (
  echo ERROR: Isolated acceptance folder already exists. Never overwrite a test or installed copy.
  exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%KIT%eng\Prepare-RcAcceptance024.ps1" -ArchiveDirectory "%CANDIDATE%" -ExpectedCommit "%EXPECTED_COMMIT%" -DestinationDirectory "%OUTPUT%"
if errorlevel 1 (
  echo ERROR: RC verification or isolated extraction failed. The app was not launched.
  exit /b 1
)
if not exist "%OUTPUT%\ACCEPTANCE-0.2.4.txt" (
  echo ERROR: Acceptance receipt missing. Refusing startup.
  exit /b 1
)
if not exist "%OUTPUT%\acceptance-user-data\" (
  echo ERROR: Private user-data directory missing. Refusing startup.
  exit /b 1
)
if not exist "%OUTPUT%\Dvinholm.App.exe" (
  echo ERROR: Isolated application executable missing. Refusing startup.
  exit /b 1
)
if /I "%~1"=="--prepare-only" (
  echo PASS: Exact-SHA isolated 0.2.4 RC prepared. WinUI was NOT launched.
  exit /b 0
)
rem An acceptance receipt also isolates direct EXE restarts without this launcher.
rem Keep the environment variable as an additional fail-closed launch check.
set "DVINHOLM_ACCEPTANCE_DATA_DIR=%OUTPUT%\acceptance-user-data"
echo Verified RC 0.2.4. Attempting to open ONLY the isolated test copy.
echo A launch attempt does not prove WinUI works. Complete the manual checklist.
start "" /D "%OUTPUT%" "%OUTPUT%\Dvinholm.App.exe"
if errorlevel 1 (
  echo ERROR: Unable to launch isolated test copy. GUI checks remain pending.
  exit /b 1
)
start "" notepad.exe "%OUTPUT%\ACCEPTANCE-0.2.4.txt"
echo Published Stable v0.2.3 has NOT been changed. RC 0.2.4 is NOT published.
exit /b 0
