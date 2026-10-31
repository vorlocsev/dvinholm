# Native x86 MSVC bootstrap without deprecated Node.js action

## Why

Development CI #4315 is green (2,059 complete-regression application tests and 8 Stable smoke), but its lone remaining annotation is the Node.js 20 deprecation warning from `ilammy/msvc-dev-cmd@v1`. Upstream v1.13.0 still targets Node.js 20 and does not have a published Node-24 replacement as of this change. Upgrading its tag alone would not fix the warning.

## Implementation

- Use the Visual Studio installation and `vswhere.exe` present on GitHub's `windows-latest` runner, requiring the x86/x64 MSVC component.
- Invoke Microsoft's `VsDevCmd.bat -arch=x86 -host_arch=x64` in `cmd.exe` and capture the successful environment in the same PowerShell CI step that runs `eng/Build-Launchers.ps1`.
- Import only the allowlisted MSVC/Windows SDK variables into that step's process, preserving unrelated GitHub runner state.
- Fail closed if Visual Studio, the MSVC component, VsDevCmd, critical variables, the exact Hostx64/x86 `cl.exe` or `rc.exe` is missing.
- Preserve the existing `/MT`, C++17, `/MACHINE:X86`, icon embedding and PE machine checks for **Launcher.exe** and **Dvinholm.exe**.
- Retain all downstream ZIP integrity, launcher self-update, rollback, sign-in recovery, checksum, packaging and Stable smoke gates.
- Extend the Windows PowerShell 5.1 syntax gate to the new bootstrap and add eight offline source-wiring contracts.

No third-party action is silently updated. No registry settings, system installs, release assets or physical hardware are modified. Only Development CI changes. Stable 0.2.4 and VirtualCar are untouched.

## Eight offline contracts

1. CI uses local bootstrap, no Node20 action.
2. PowerShell 5.1 parser includes the new script.
3. Visual Studio discovery requires installed MSVC components.
4. VsDevCmd explicitly selects Hostx64/x86.
5. Missing/failed setup aborts compilation.
6. Only allowlisted compiler environment is imported, not GitHub settings.
7. `cl.exe` and `rc.exe` are checked before build, and failures are nonzero.
8. Native launcher /MT/x86/ICO checks and downstream packaging gates remain.

Final acceptance requires a successful exact-head PR Windows x86 CI with zero Node20 warnings **and** an independent complete main CI after merge, including archived MSIX tests. Local source tests cannot substitute for the real native compilation in CI.
