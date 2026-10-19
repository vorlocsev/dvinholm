# ECU profile search — Windows manual smoke checklist

**Feature:** [PR #367](https://github.com/vorlocsev/dvinholm/pull/367), merged into `main` as `15f43e16fa9a697a857bdfa46e4bbd8e71e4e38c`.

**Hardware:** no physical J2534 adapter required. Use a **successful `main` CI build newer than v0.2.1**, since v0.2.1 itself does not contain the profile search panel. Keep all tests disconnected. This checklist is **not** a claim of completed manual verification.

| # | Check | Expected result | Status |
| - | - | - | - |
| 1 | Start the new build on Windows. | The ECU panel shows **Find ECU profile**, **Marked verified only**, **ECU profiles**, **Add test profiles**, **Import JSON** and **Export JSON**. CAN Monitor remains visible. | ☐ |
| 2 | Click **Add test profiles** with a new or existing catalog. | Two synthetic profiles appear, with no duplicate entries on repeated clicks. | ☐ |
| 3 | Search `standard`, then `0x7e8`. | Only **TEST — Standard CAN (11-bit)** appears. Search is case-insensitive and accepts `0x`. | ☐ |
| 4 | Search `F190`, then `0x18DAF110`. | `F190` matches the standard test profile; the extended response ID matches **TEST — Extended CAN (29-bit)**. | ☐ |
| 5 | Clear search and enable **Marked verified only**. | Neither built-in synthetic profile appears, because both are unverified. The description explains that this flag comes from the catalog author. Disable the filter to restore them. | ☐ |
| 6 | Select the standard profile. Manually edit its UDS response ID and search `standard`. | Because the selected profile stays visible, the manually edited field remains unchanged. No diagnostic request is sent. | ☐ |
| 7 | With the standard profile selected, search `extended`. | Hiding the selected profile clears stale request/response CAN IDs, DID and addressing. Selecting the extended profile then fills its own saved values. | ☐ |
| 8 | Clear search; switch between extended and standard profiles. | CAN IDs, 11-/29-bit format and suggested DID correspond to the newly selected profile; selecting standard again restores `7E0/7E8` and `F190`. | ☐ |
| 9 | Select a test profile and read the metadata section. | The source note, DIDs, CAN IDs and unverified status are readable and selectable, can be scrolled if needed, and do not push CAN Monitor off-screen. | ☐ |

### Evidence to record

- Windows version and installed Dvinholm CI number / seven-character SHA.
- Any clipped controls or unusual selection behavior.
- Which checks passed or failed. If needed, share a redacted screenshot with no VIN, personal paths or other identifiers.

**Limitation:** these offline checks do not verify J2534 drivers, physical CAN signals, ECU addressing, or compatibility with any Volvo vehicle. **Stable v0.2.1 stays unchanged.**
