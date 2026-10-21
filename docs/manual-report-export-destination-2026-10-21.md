# Export destination safety — Windows manual acceptance checklist

**Build:** Newer Development CI after the report export destination guard PR. This is not in stable v0.2.2.  
**Status:** **Regression found by user:** earlier WinRT Save picker created a zero-byte placeholder on rejected destinations. Fix in a newer Development build still **PENDING user verification**. Do not mark any step complete solely because CI passes.  
**Scope:** Protect new TXT/CSV report content from the installed application's directory. No J2534 adapter or vehicle is needed for synthetic checks.

- [ ] 1. Update to the new successful main CI build through **Development**. Confirm the ordinary synthetic UDS preview and CAN Monitor demo remain accessible.
- [ ] 2. Try **Export synthetic UDS TXT** targeting the same directory as `Dvinholm.App.exe`. Expect **Report not saved: choose a folder outside the Dvinholm application directory**, with **NO file created at all**. In older CI #2905 the WinRT Save picker could leave a zero-byte placeholder; this is the regression being fixed.
- [ ] 3. Try exporting a synthetic UDS TXT to a **subfolder** under the application directory. Confirm the same refusal and that **no new zero-byte placeholder** was created there.
- [ ] 4. Save the same synthetic UDS TXT to a normal external folder such as **Documents**, and confirm that it opens and retains its synthetic warning and fictional F190/F187/DTC values.
- [ ] 5. Start CAN Monitor **Start demo**, then try **CSV log** targeting the application folder. Confirm writing report content is rejected and the demo keeps running.
- [ ] 6. Export a demo **CSV log** to Documents. Confirm the CSV has `source` provenance and `SYNTHETIC_DEMO` on rows. Stop demo afterwards.
- [ ] 7. Verify ordinary **Cancel** behavior in both Save As dialogs. No new or empty files should be created, and a pre-existing saved TXT/CSV must not be truncated or changed on Cancel or blocked path selection.
- [ ] 8. Fully restart Dvinholm. Confirm no new files were written automatically inside its installation folder, that ECU profiles persist and existing TXT/CSV in Documents remain available.

**Boundary:** A string-based check of user-selected paths prevents ordinary accidental writes. Earlier Windows.Storage.Pickers.FileSavePicker created a zero-byte placeholder before application validation (user-observed regression). The replacement Win32 Save As dialog is intended to return only a path, without materializing any file first; junctions, hard links and reparse-point aliases remain outside this ordinary path-string guard's security boundary. This is not a substitute for backing up personal reports, and it does not establish physical J2534/ECU/vehicle compatibility.
