# CAN Monitor buffered history — verification checklist

**Change:** [PR #376](https://github.com/vorlocsev/dvinholm/pull/376), merged to `main` as `2dec8831b63c0c5306694dd21209ce8b2a19e07d`.

**Automated CI:** [PR run #2862](https://github.com/vorlocsev/dvinholm/actions/runs/39342233407) succeeded. Seven automated tests cover the bounded, software-filter-independent receive buffer and virtual J2534 injection.

**Important:** `VirtualJ2534Adapter` is an in-process .NET test fixture, **not** a selectable WinUI adapter. On a machine without a physical J2534 adapter, **Start monitor remains disabled**; this is expected. The following Windows UI checks are future/pending acceptance tests. No physical adapter or vehicle has been used.

| Check | Action (once a safe, isolated synthetic UI CAN feed exists or a compatible real adapter is available) | Expected result |
|---|---|---|
| 1 | Start CAN Monitor with synthetic receive stream containing `7E0`, `7E8`, `123`. | IDs, type, DLC, data display with correct ordering and RX count. |
| 2 | Enter ID filter `7E8`. | Only matching frames shown. RX total remains cumulative. |
| 3 | Remove the ID filter without restarting monitor. | Previously hidden `7E0` and `123` rows reappear from the bounded history. |
| 4 | Enter `0`, then an invalid hex filter. | Zero matches only actual CAN ID zero; malformed filter shows no rows but retains history. |
| 5 | Receive more than 5000 frames while a restrictive filter is active, then clear filter. | Only last 5000 frames are buffered. Older rows cannot reappear. |
| 6 | Export CSV with filter active, then open it. | CSV contains only currently visible rows with timestamp, ID, type, DLC and data. No write failure. |
| 7 | Click Clear while receiving, then stop/restart monitor. | History/view/count reset; new frames appear normally; no unintended diagnostic TX. |

Automated tests already verify steps corresponding to filtered replay, malformed/zero filters, bounded history and virtual RX. They do **not** prove actual Windows UI interaction or CSV file picker behavior. Keep real hardware testing deferred.
