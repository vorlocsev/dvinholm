# Archived MSIX negative validator test isolation (Development CI)

## Why this fix is needed

On the post-merge **CI #4309** for `main` commit `5060492`, the complete x86 regression suite finished with **2047 passed / 3 failed of 2050**. The three failures were archived `WindowsLiteMsixNegativeContractTests` cases (`wrong-identity`, `no-manifest`, `missing-visual-asset`), each hitting a **60-second** `Process.WaitForExitAsync` cancellation while launching a real inbox Windows PowerShell 5.1 validator. The application built, and PR-only active tests had passed. The log does not establish a MSIX contract violation or a J2534/CAN defect.

The main/manual CI deliberately runs archived workflow tests that are excluded from PR active tests. Consequently, subprocess contention can surface only **after** a merge.

## Fix

- Keep the eight existing negative fixture cases and **all the original MSIX rejection assertions**. Do not disable, skip or extend their 60-second execution bound.
- Assign the archived validator class to an xUnit collection with `DisableParallelization = true`. Its Windows PowerShell subprocesses no longer compete with other xUnit test collections during the complete-regression run.
- If the timeout still occurs, terminate the child process tree, perform a bounded five-second exit wait, and report the exact fixture scenario as a `TimeoutException`.
- Retain best-effort deletion of uniquely named temporary fixtures in `finally`. This does not install MSIX or change any system package.
- PR CI still runs its normal active tests; the important additional acceptance gate is the next **full main/manual test run** with all archived scenarios. The failed CI #4309 attempt remains historical evidence and is not silently treated as green.

## Eight intact offline contract cases

1. Missing manifest.
2. x64 manifest instead of x86.
3. Wrong package identity.
4. Wrong executable declaration.
5. Missing executable payload.
6. Bundled runtime in a framework-dependent package.
7. Missing required visual asset.
8. Debug files in the package.

No app runtime/ECU/CEM code, VirtualCar, published Stable 0.2.4 files or release state changes. No physical J2534/DiCE test is inferred from this CI correction.
