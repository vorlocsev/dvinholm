# Synthetic UDS TXT export — Windows offline acceptance plan

**Version:** Newer Development builds after PR #387; **not included in stable v0.2.2**.  
**Status:** **8/8 checks user-confirmed on 2026-10-21** in Windows on published **Development CI #2899** (`94ab300`). Evidence: [PR #388 conversation](https://github.com/vorlocsev/dvinholm/pull/388). Automated CI is additional evidence, not a substitute for Windows observation.  
**Scope:** A fixed fictional UDS sample; no J2534 adapter, ECU, vehicle or real VIN.

- [x] **Test 1:** With J2534 disconnected and CAN Demo stopped, verify **Export synthetic UDS TXT** appears beside **Preview UDS example (offline)**. Real Read DID/VIN/DTC buttons remain disabled.
- [x] **Test 2:** Click **Export synthetic UDS TXT** and inspect the Windows Save dialog; the proposed name includes `uds-SYNTHETIC-example` and the extension is `.txt`.
- [x] **Test 3:** Press **Cancel**. Confirm no TXT file was created, existing status/preview is untouched and Dvinholm stays running.
- [x] **Test 4:** Click Export again; choose a writable folder **outside the application installation directory**, save the TXT, and verify **SYNTHETIC UDS TXT saved** appears in the status area.
- [x] **Test 5:** Open the saved TXT in a plain-text editor. The first line should read **Dvinholm — OFFLINE SYNTHETIC UDS TXT REPORT**; the file must say **no adapter, ECU or vehicle was accessed** and **not a diagnostic report**.
- [x] **Test 6:** Verify that the text contains fictional F190 VIN `TEST0000000000ABC` with original bytes, F187 `DEMO-ECM` marked unverified, and two DTC statuses 0x08 confirmed and 0x04 pending. No real or vehicle-specific data should appear.
- [x] **Test 7:** Start CAN Monitor **Start demo**, click Export and verify it refuses because a disconnected idle session is required. Stop demo; verify export may be used again.
- [x] **Test 8:** Close and reopen Dvinholm; verify there is no automatic TXT creation/export. The saved TXT remains at the chosen location, while real Read DID/VIN/DTC remain disabled without a session.

**Security/provenance:** The export action builds a new report only from `UdsOfflineSample.Format()` rather than reading live status text. Cancelling the picker performs no file write. Real diagnostic data export is not implemented.

**Windows acceptance — completed:** user confirmed update to `CI #2899 · 94ab300`; presence of export button and disabled live UDS actions; native Save dialog with `uds-SYNTHETIC-example.txt`; Cancel without file creation; actual user-selected TXT write outside the application directory; disclaimer and saved fictional VIN F190, DID F187, two DTCs and original bytes; blocking during active CAN Demo and restoration after Stop; no auto-export and saved-file persistence across full application restart. [Success evidence in PR #388](https://github.com/vorlocsev/dvinholm/pull/388) including final test checkpoint. Software-only scope; **no J2534 adapter, ECU or vehicle testing**. The separately published stable release remains **v0.2.2**.
