# XC90 P2/CEM compatibility evidence matrix (Development 0.3.0)

This is a **read-only/offline source register** in the existing
**P2 configuration candidates (offline only)** dialog. Select a *claimed*
model year, network, proposed CAN bitrate and interface, then press
**Review legacy transport risks (offline)**. The additional matrix is
generated locally from fixed source-scoped policy. It never discovers an ECU,
queries a vehicle, changes adapter settings, reads a PIN or writes anything.

## Evidence and compatibility scope

| Dimension | Source-backed reference | Required before live CEM configuration read |
| --- | --- | --- |
| XC90 model/year | Volvo's official [VIDA platform list](https://volvotechinfo.com/vida) identifies **P2 XC90 type 275, MY2003–2014** | Independently verify exact vehicle/module year; selecting a year is a claim |
| HS-CAN | Existing source-limited workshop advisory: through MY2004 **250 kbit/s**; MY2005 onward **500 kbit/s** | Verify physical HS-CAN identity; rate is NOT CEM addressing, nor applicable to LS/MS CAN or K-line |
| VIDA communication tool | [Volvo Special Tools](https://www.volvospecialtools.com/index.cfm?event=info.dice) recommends DiCE for older D2/GGD; says **J2534-1 cannot do VIDA diagnostics**; J2534-2 can handle certain software downloads | Hardware/driver and protocol-specific CEM acceptance on an authorized vehicle; generic compatibility alone is not proof |
| CEM hardware/software | No authenticated production identity entry | Independently sourced and corroborated CEM hardware, software and diagnostic-software revisions |
| CEM transport and addresses | No validated production CEM request/response ID, network or K-line mapping | Authorized physical reproduction of exact transport and addresses |
| Configuration read mechanism | Generic UDS `0x22` DID snapshots exist, but are opaque and **not established as CEM configuration** | Per-revision read service, identifier semantics, length/negative-response behavior and trusted provenance |
| Recoverable backup | DID export has a checksum but is **not a restorable backup** | Tested raw backup, restore and recovery on exact revision |
| PIN/authentication/write | No authorized CEM service or writer implemented | Separate entitlement, protocol evidence, safe state/recovery and supervised bench tests |

The first three dimensions are **reference guidance**, not independently
verified observations on the specific vehicle. The remaining dimensions
are unverified or unavailable. Even a year/bitrate match and a DiCE
selection never unlock a read/write operation.

The UI reuses `VolvoP2CemHsCanSpeedAdvisory` for year-scoped HS-CAN
and the preexisting `VolvoP2CemTransportResearchPreflight`; it does
not invent CEM IDs, read DIDs, seed-key steps or PIN algorithms.
The new `VolvoP2CemReadinessMatrix` reports eight distinct dimensions
and permanently denies any CEM request, PIN acquisition, restore or write.

## Next actionable evidence intake

1. Independently check XC90 model year and CEM hardware/software identifiers
   from an authorized diagnostic session; keep VIN/PIN/serial data out of
   project issues or synthetic tests.
2. Record one specific source for that exact module revision's documented
   diagnostic transport and configuration **read** semantics. A generic
   profile, vendor menu label or VIDA logical node number is insufficient.
3. Use the existing manual VIDA card and offline diagnostic snapshot
   comparison to identify discrepancies; do not treat a pair of user-editable
   files as an authenticated same-module match.
4. Keep the production CEM identity and config-read allowlists empty until
   independently corroborated documentation **and** authorized physical
   tests are available. No hardware test is claimed by this PR.

Eight offline checks cover year boundary, source-limited bitrate, mismatch,
network separation, J2534-1, DiCE/J2534-2, module/addressing evidence
and non-executable report/UI safety.
