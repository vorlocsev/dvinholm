# Export destination safety — Windows manual acceptance checklist

**Build:** Newer Development CI after the report export destination guard PR. This is not in stable v0.2.2.  
**Status:** Pending manual Windows confirmation. Do not mark a step complete solely because CI passes.  
**Scope:** Protect new TXT/CSV report content from the installed application's directory. No J2534 adapter or vehicle is needed for synthetic checks.

- [ ] 1. Update to the new successful main CI build through **Development**. Confirm the ordinary synthetic UDS preview and CAN Monitor demo remain accessible.
- [ ] 2. Try **Export synthetic UDS TXT** targeting the same directory as `Dvinholm.App.exe`. Expect **Report not saved: choose a folder outside the Dvinholm application directory** rather than writing report contents. The Save picker may leave a placeholder; remove it if present.
- [ ] 3. Try exporting a synthetic UDS TXT to a **subfolder** under the application directory. Confirm the same refusal; no report content should be written there.
- [ ] 4. Save the same synthetic UDS TXT to a normal external folder such as **Documents**, and confirm that it opens and retains its synthetic warning and fictional F190/F187/DTC values.
- [ ] 5. Start CAN Monitor **Start demo**, then try **CSV log** targeting the application folder. Confirm writing report content is rejected and the demo keeps running.
- [ ] 6. Export a demo **CSV log** to Documents. Confirm the CSV has `source` provenance and `SYNTHETIC_DEMO` on rows. Stop demo afterwards.
- [ ] 7. Verify ordinary **Cancel** behavior in both Save pickers and that the saved TXT and CSV outside the app remain untouched; no extra report is written automatically.
- [ ] 8. Fully restart Dvinholm. Confirm no new files were written automatically inside its installation folder, that ECU profiles persist and existing TXT/CSV in Documents remain available.

**Boundary:** A string-based check of user-selected paths prevents ordinary accidental writes. Windows pickers can create a placeholder before application validation; junctions, hard links and reparse-point aliases are not a security boundary. This is not a substitute for backing up personal reports, and it does not establish physical J2534/ECU/vehicle compatibility.
