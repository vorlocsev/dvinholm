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

## Prioritized next-evidence checkpoint (Development)

The same offline report now prints **one next evidence checkpoint**, selected
from the existing eight-dimension compatibility matrix. This is a research
worksheet prompt, never an authorization gate. Priority follows confirmed
XC90 P2 model-year scope, the documented J2534-1 VIDA limitation, physical
network identity, HS-CAN reference rate mismatch or missing rate, diagnostic
interface evidence, and finally corroboration of exact CEM hardware,
application-software and diagnostic-software revisions.

For example, a claimed MY2009 XC90 / HS-CAN 500 kbit/s / DiCE combination
asks for independent **CEM revision evidence**, not a CAN address, PIN or
configuration-read command. Choosing J2534-2 instead asks for supported
physical diagnostic-interface evidence; a software-download capability is
not proof of working CEM diagnostics. An unconfirmed or out-of-scope year,
J2534-1 VIDA diagnostics or conflicting HS-CAN speed takes precedence over
revision review. No claim or checkbox makes a physical CEM authenticated.

Eight synthetic xUnit checks cover missing/out-of-scope year, incompatible
VIDA interface, unidentified/non-HS-CAN network, missing/conflicting
HS-CAN rate, separate DiCE/J2534-2 outcomes, privacy and permanent service
denial. The current WinUI report already calls the matrix, so no new picker
or runtime connection is added. Real DiCE/J2534/ECU testing remains pending.

## Read-only fingerprint catalog integrity (Development)

The separate `VolvoP2CemEvidenceCatalogValidator` now limits any future
XC90 P2/CEM research signature to the documented **MY2003–2014** scope,
reusing the same model-year boundaries as the manual VIDA worksheet and
HS-CAN advisory. A production or build date in calendar 2002 is **not** a
verified MY2002 identity. Entries that extend into MY2002, MY2015 or
outside the published XC90 P2 range are rejected, not partially clipped.

The direct `VolvoP2CemSupportMatrix.Assess` path additionally rejects
model years outside this range and snapshots with absent timestamps or
non-UTC offsets. That matters even when a caller bypasses the existing
checked UI `CaptureIdentityReview` wrapper. Synthetic rules may still
match a valid UTC in-scope DID snapshot in offline tests, but they never
authenticate a physical ECU or enable a CEM read, PIN service or write.
The bundled production catalog remains **empty**.

Eight synthetic xUnit gates exercise year boundaries, broadened ranges,
direct matcher rejection, missing/non-UTC capture timestamp and normal
read-only behavior. No real adapter/vehicle, ECU revision, config bytes
or new protocol mapping was verified.

## Physical CEM identity vs a matching research signature

The offline multi-DID matcher may label a match as
`VerifiedIdentityReadOnly` or `DocumentedReadOnlyDidSet` **relative to the
supplied test/research catalog**. That verdict never authenticates where a
capture came from, proves it was a physical XC90 P2 CEM, or establishes
the provenance of a software revision. The built-in production identity
catalog remains empty.

The CEM service-capability checklist now makes this distinction explicit:
`VolvoP2CemSupportAssessment.HasAuthenticatedPhysicalCem` remains
**false** for every existing result, including synthetic fingerprint
matches. Consequently the **ExactCemHardwareSoftwareIdentity** evidence
gap stays open across **all six** future service categories, even with
self-reported VIDA readings, claimed source references, nominal bench
notes and a matching DID pattern. The operational service gate also
remains closed. No input string, checkbox or test fixture makes a
physical CEM trusted.

Eight synthetic regression tests cover production-empty status,
identity-only and multi-DID matches, all future services, forged
variant labels, year provenance, claimed bench evidence and full
hypothetical source references. The existing service checklist
contracts were updated to expect this previously omitted identity
gap. No real adapter, ECU, PIN or configuration operation was tested
or implemented.

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
