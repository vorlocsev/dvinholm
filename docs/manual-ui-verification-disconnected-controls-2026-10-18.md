# Manual Windows UI verification — disconnected controls (2026-10-18)

- User confirmed all four controls — **Start Monitor**, **Read VIN**, **Read DTC**, and **Read DID** — are inactive while the app is disconnected.
- This matches the expected safety gate: CAN Monitor and read-only UDS actions are unavailable without an active J2534/CAN session.
- No Connect action was used. No physical J2534 adapter or vehicle was available or tested; hardware compatibility remains unverified and deferred.
- This is a manual UI smoke check only; it does not verify the controls transition to enabled after a successful connection.
- Continue software-only release checks. Do not publish another release without explicit user authorization.

Related checkpoint: [PR #282](https://github.com/vorlocsev/dvinholm/pull/282), which records J2534 DLL-path persistence and disconnected startup behavior in `ROADMAP.md` and `AI_WORKLOG.md`.
