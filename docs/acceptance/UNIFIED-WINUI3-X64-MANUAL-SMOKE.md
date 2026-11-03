# Dvinholm unified x64: Windows installation and J2534 manual acceptance

**Status: pending hands-on Windows review.** This is an acceptance checklist, not a record of successful physical adapter tests. PR-only CI ZIPs are not signed releases, and production update replacement stays disabled.

## Preparation

Use an isolated Windows 11 x64 test machine with a known restore point / disposable installation. Obtain the ZIP only from the GitHub Actions artifact for the **exact PR head commit** whose automated x64 pilot and full regression runs both passed. Do not treat successful publishing as Windows GUI acceptance. Never use an actual vehicle or live CEM PIN/configuration service for this checklist.

## Eight bounded checks (record PASS / FAIL / NOT RUN)

1. **Package and executable.** Confirm the reviewed x64 ZIP passes published SHA-256 and extraction checks. Run from a clean user-writable directory, not from inside the ZIP. Confirm there is one primary `Dvinholm.exe` window; the old separate launcher is not required in this new x64 pilot. This is not a certificate-signing test.
2. **Start on clean Windows.** Verify WinUI 3 and the bundled .NET 10 self-contained files launch without unplanned installers or runtime-download prompts. If installation fails, capture sanitized exception type and Windows build only, not usernames or private file paths.
3. **Approved Aurora pages.** Verify the six primary destinations: Connection, CEM PIN, CEM configuration, Faults (DTC), Modules (ECU), Updates. Check scrolling at 100% and 125% Windows scale and ensure the removed Settings destination is absent.
4. **Language and updates.** Switch RU/EN twice and verify titles, driver audit, CEM identity status, source filter and integrated update controls change language without resetting the user-selected update channel. Download/staging must not automatically apply unverified files. The install action stays blocked.
5. **J2534 registry audit.** On the Connection page expand **Driver architecture audit**. Record only the counts and category labels. Verify Refresh re-runs a read-only scan and the result never prints any absolute DLL paths or registry subkey paths. A reported PE x64 match is **not** proof of the cable or J2534 API working.
6. **Wrong architecture and fallback.** If a registered x86 vendor DLL is available, verify it is reported as wrong bitness and is not auto-selected by the x64 application. If a vendor installer registers a PE-x64 DLL only under the x86 registry view, verify it is reported as a registry mismatch. Never force-load a synthetic or wrong-bitness DLL.
7. **CEM safety.** On CEM PIN run **Inspect prerequisites locally**; the result must list eight conditions and issue no CAN/J2534 requests. Switch to CEM configuration: third-party and Volvo menu labels remain in separate tiers, no live vehicle values appear, and configuration backup/edit/write are disabled. A user profile or generic DID capture must not authenticate physical CEM identity.
8. **Session lifecycle and finish.** With the test adapter or virtual fixture only, observe connect/disconnect, cancellation, and page navigation. A failed or cancelled request must not leave stale identity or PIN status. Close the application and verify no update helper remains running. Record any defects, log references and the exact commit SHA in the PR without copying VIN, PIN, DLL paths, tokens or other secrets.

## Vendor/physical acceptance intentionally not performed

Actual x64 vendor support for **Volvo DiCE** and **Mongoose JLR Pro** remains unknown until a reviewed driver is installed and tested on Windows x64. Driver registry registration, PE architecture, successful DLL load, J2534 API conformance, CAN monitor RX and actual vehicle responses are separate acceptance levels. Physical vehicle and CEM procedures require a later, separately authorized stage with independently validated addressing and safe read-only service semantics.

**Result template:** commit SHA; Windows build; x64 pilot run ID; full regression run ID; test 1–8 = PASS/FAIL/NOT RUN; anonymized symptom and reproducer; operator/date. Do not mark a release ready while required checks remain NOT RUN.
