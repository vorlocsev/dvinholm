# Development / Stable — Windows manual acceptance record

**Date:** 2026-10-20  
**Build:** Published Windows x86 CI #2884, commit `651e68b05def367a095d535591631846ade30f42`.  
**Prior installation:** Stable v0.2.2 at `77b6899b2c4dfca283463cd8337f447e1e44d278`.  
**Scope:** Software-only Windows UI, GitHub updater and persisted ECU metadata. **No J2534 adapter, ECU or vehicle**.

| # | Windows action | User-confirmed observation |
|---|---|---|
| 1 | On v0.2.2, choose **Development → Check update** | Offered CI #2884 (`651e68b`) |
| 2 | Click **Replace files and restart** | Files replaced; Dvinholm restarted with newer build |
| 3 | Inspect source selector in CI #2884 | Exactly **Development** and **Stable**, Development selected by default |
| 4 | Development → **Check update** | **Up to date**; latest CI #2884 at matching SHA |
| 5 | Stable → **Check update** | Latest published stable v0.2.2 (`77b6899`) offered; no rollback installed |
| 6 | Switch Stable → Development | Previous Stable candidate discarded; button reverts to **Check update** |
| 7 | Open existing ECU Profiles after update/restart | `user.demo.v022.ecm` retained edited name, `7E0/7E8`, `F187,F190`, original source, and Unverified status |
| 8 | Check built-in test profiles | Standard (11-bit) and Extended (29-bit) synthetic profiles remain present |

All eight scenarios above were **reported successful by the user** and captured in [PR #383 discussion](https://github.com/vorlocsev/dvinholm/pull/383). This is manual confirmation, not a claim that the application was operated by CI.

**Important limits:** Unlike the installed newer Development build, stable v0.2.2 is an older commit; user intentionally **did not press** Replace files and restart under Stable. Commit SHA mismatch alone is not a proof of version ordering, so a future warning/confirmation should establish whether a candidate is older. We have not validated installing or recovering from a downgrade. No hardware support is asserted.

CI evidence: [#2883](https://github.com/vorlocsev/dvinholm/actions/runs/39456524973) and [#2884](https://github.com/vorlocsev/dvinholm/actions/runs/39457307648) both passed. Stable v0.2.2 remained unchanged.
