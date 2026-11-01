# Synthetic ECU identity evidence catalog boundary

Development 0.3.0. The generic read-only identity matcher processes a
catalog of independently researched DID signatures. The existing P2/CEM
catalog validator already limited its entries, but the lower-level matcher
could be called directly with an unbounded enumerable or a mixture of valid
and malformed rules. Skipping invalid entries could cause an apparently
unique identity result after conflicting evidence was discarded.

The matcher now limits direct input to **128 rules** (examining at most 129),
requires the **entire** catalog to pass structural eligibility, and rejects
noncanonical CAN addressing and excessively long/control-bearing attribution.
An invalid or oversized catalog results in UNKNOWN, not a match. Observed
snapshots also need valid addressing and at most 128 DIDs.

An individual matching synthetic rule still yields an explicitly **read-only
identity classification**, not authorization. Multiple matching rules remain
AMBIGUOUS. No Volvo P2/CEM hardware/software signature, real diagnostic CAN
address, verified DID definition, PIN decoding, configuration writing or
service permission has been introduced. Production CEM catalog stays EMPTY.

Eight deterministic tests cover single/empty/maximum/overflow catalogs,
nonmatching invalid entries, malformed addresses/source strings, invalid
snapshot structure and genuine ambiguity. These are offline tests only;
installed Windows and real DiCE/J2534/CEM acceptance remain outstanding.
Stable v0.2.4 is unchanged.

## Direct generic matcher UTC and provenance check

The **generic** `EcuIdentityEvidenceMatcher.Assess` is callable directly,
not only through the existing XC90 P2 CEM UI and support matrix. Before
classifying any user-supplied multi-DID research fingerprint, it now requires
a **non-default UTC capture timestamp with zero offset**. A missing timestamp
or a capture labelled with a non-UTC offset produces UNKNOWN, never
`VerifiedReadOnlyIdentity`. Existing CAN format, distinct DID, catalog-size
and VIN-exclusion rules remain in force.

A matching `IsVerified` flag plus an `EvidenceSource` text label still does
**not** independently authenticate that source, physical ECU or vehicle.
The generic matcher explanation now describes the event as a **supplied
research fingerprint match** and explicitly says physical identity is
**unverified**. Attribution strings, user profile names, DID bytes and
other private metadata are not repeated in the output.

Eight synthetic unit checks cover absent/non-UTC capture times, UTC
research-only matching, untrusted source flags, invalid catalog rules,
malformed observed DIDs, ambiguous matches, and spoofed profile
verification. No adapter, CAN request, real CEM signature or PIN service is
added. The stricter generic boundary complements the independent,
permanently denied physical CEM identity gate.

## CEM research matcher display terminology

The CEM-specific `VolvoP2CemSupportMatrix` consumes **supplied,
editable research evidence** and the generic offline fingerprint matcher.
Its legacy internal enum value `VerifiedIdentityReadOnly` means the DID
pattern matched a structurally eligible supplied rule, **not** that the
underlying rule, physical ECU, source provenance or adapter session was
independently authenticated. That enum is retained for existing caller
compatibility; it must not be displayed as a physical-identity verdict.

The human-facing `Explanation` for **every** assessment state, including
identity-only and previously observed DID matches, now explicitly describes
research-only data and an **unverified physical CEM**. In particular, an
`IsVerified` flag, an `EvidenceSource` string, a profile name and a
candidate variant label do not create authentication. Private reference
strings and DID payloads are not inserted into the explanation.

Eight synthetic regression tests cover production-empty results, matching
research identity only, previously observed DID claims, forged source and
profile labels, unsupported model years, ambiguity, invalid catalogs and
the separate live-capture presentation. All existing CEM PIN, config read,
backup and write gates remain disabled; production signatures remain empty.
