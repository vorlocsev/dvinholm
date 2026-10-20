# Offline Demo CAN Monitor — Windows 8-step smoke checklist

**Build requirement:** a successful `main` CI build at or after merge commit `8b8c89c6df18044dd10d3b0d4d85ba8ba46d31ff`, containing [PR #378](https://github.com/vorlocsev/dvinholm/pull/378). Stable v0.2.1 lacks Demo CAN Monitor.

**Hardware:** none. Keep the J2534 adapter disconnected. This mode is exclusively synthetic, not a virtual J2534 session and not a vehicle diagnostic test. The checks below are **pending manual Windows confirmation**.

| # | Action | Expected result | Status |
|---|---|---|---|
| 1 | Run Dvinholm without an adapter. Observe **Start monitor** and **Start demo**. | Real Start monitor, Read VIN/DID/DTC remain disabled; **Start demo** is enabled. | ☐ |
| 2 | Click **Start demo**. | Visible **DEMO ACTIVE** banner and rows with IDs `7E0`, `7E8`, `18DAF110` (29-bit), `000`. Frame count/rate increment; Connect is disabled. | ☐ |
| 3 | Type `7E8` into ID filter. | Only `7E8` is shown; received-frame counter continues increasing. | ☐ |
| 4 | Clear the filter, enter `0`, then enter invalid `ZZZZ`, and finally clear again. | Previously hidden rows return; `0` matches only zero-ID samples; invalid filter shows no rows but does not erase history. | ☐ |
| 5 | With filter `7E8` active, click **CSV log** and save. Inspect the file. | Export contains only filtered rows; header ends with `source`, every data row ends in `SYNTHETIC_DEMO`, and filename suggests `can-demo-SYNTHETIC`. | ☐ |
| 6 | Click **Clear** while demo is still running, then observe the counters. | Counts and history reset; fresh demo frames begin accumulating again. | ☐ |
| 7 | Click **Stop demo**, then change ID filter and inspect retained rows; finally click Clear. | Timer stops, rows remain available for refilter/CSV until Clear; after Clear, banner/data are removed and Connect is enabled. | ☐ |
| 8 | Close and restart the app without an adapter. | Demo is not started automatically; real J2534/UDS controls remain disabled; ordinary live CSV format unchanged. | ☐ |

**Safety/verification boundary:** no real CAN message is transmitted by demo mode. Automated tests validate the pure generator, monitor history and CSV formatting; **they do not substitute for the above WinUI interaction checks**. Real adapter/vehicle tests remain deferred.
