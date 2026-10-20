# Offline Demo CAN Monitor — Windows 8-step smoke checklist

**Build requirement:** a successful `main` CI build at or after merge commit `8b8c89c6df18044dd10d3b0d4d85ba8ba46d31ff`, containing [PR #378](https://github.com/vorlocsev/dvinholm/pull/378). Stable v0.2.1 lacks Demo CAN Monitor.

**Hardware:** none. The user completed the eight principal offline Windows smoke scenarios on 2026-10-20 without an adapter. This mode is exclusively synthetic, not a virtual J2534 session and not a vehicle diagnostic test. See evidence in [PR #379](https://github.com/vorlocsev/dvinholm/pull/379).

| # | Action | Expected result | Status |
|---|---|---|---|
| 1 | Run Dvinholm without an adapter. Observe **Start monitor** and **Start demo**. | Real Start monitor, Read VIN/DID/DTC remain disabled; **Start demo** is enabled. | ✓ User-confirmed: disconnected mode and controls, verified again after restart |
| 2 | Click **Start demo**. | Visible **DEMO ACTIVE** banner and rows with IDs `7E0`, `7E8`, `18DAF110` (29-bit), `000`. Frame count/rate increment; Connect is disabled. | ✓ User-confirmed: synthetic frames generated and displayed |
| 3 | Type `7E8` into ID filter. | Only `7E8` is shown; received-frame counter continues increasing. | ✓ User-confirmed: `7E8` only, RX grows |
| 4 | Clear the filter, enter `0`, then enter invalid `ZZZZ`, and finally clear again. | Previously hidden rows return; `0` matches only zero-ID samples; invalid filter shows no rows but does not erase history. | ✓ User-confirmed: history restored; zero-ID and `ZZZZ` behavior |
| 5 | With filter `7E8` active, click **CSV log** and save. Inspect the file. | Export contains only filtered rows; header ends with `source`, every data row ends in `SYNTHETIC_DEMO`, and filename suggests `can-demo-SYNTHETIC`. | ✓ User-confirmed: saved, opened and inspected tagged, filtered CSV |
| 6 | Click **Clear** while demo is still running, then observe the counters. | Counts and history reset; fresh demo frames begin accumulating again. | ✓ User-confirmed: reset, then new frames and counters resume |
| 7 | Click **Stop demo**, then change ID filter and inspect retained rows; finally click Clear. | Timer stops, rows remain available for refilter/CSV until Clear; after Clear, banner/data are removed and Connect is enabled. | ✓ User-confirmed: stopped, history refiltered, Connect enabled, marker cleared |
| 8 | Close and restart the app without an adapter. | Demo is not started automatically; real J2534/UDS controls remain disabled; ordinary live CSV format unchanged. | ✓ User-confirmed: default-off and disconnected controls after restart; real CSV schema covered by CI only |

**Safety/verification boundary:** no real CAN message is transmitted by demo mode. Automated tests validate the pure generator, monitor history and CSV formatting; **they do not substitute for the above WinUI interaction checks**. Real adapter/vehicle tests remain deferred.

## Verification record — 2026-10-20

All eight primary manual UI scenarios above were user-confirmed across sequential checkpoints on [PR #379](https://github.com/vorlocsev/dvinholm/pull/379). Demo CSV was actually saved/opened and its `SYNTHETIC_DEMO` provenance checked. The full restart confirmed default-off, empty monitor and disabled real diagnostic controls. The exact locally installed `Current` CI/SHA was not reported; the separate live-CAN CSV format was covered by automated tests but was not manually checked using a real adapter. **No real vehicle or J2534 adapter testing occurred.**

Automated regression and packaging evidence: PR #378 [CI #2868](https://github.com/vorlocsev/dvinholm/actions/runs/39344455511), post-merge [main #2869](https://github.com/vorlocsev/dvinholm/actions/runs/39345037152), documentation PR #379 [CI #2871](https://github.com/vorlocsev/dvinholm/actions/runs/39345289595), and [main #2872](https://github.com/vorlocsev/dvinholm/actions/runs/39345796252) all succeeded. Stable release stays v0.2.1.
