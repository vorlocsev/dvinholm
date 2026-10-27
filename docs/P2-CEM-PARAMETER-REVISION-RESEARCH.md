# XC90 P2/CEM parameter-to-revision evidence research

Status: **development-only metadata preflight, no validated physical CEM
configuration parameters, no real ECU access or vehicle editing**.

## Why this exists

The high-level eight-option `VolvoP2ConfigOptionCatalog` was built from
public vendor-advertised categories. It does **not** contain production Volvo
option identifiers, byte locations, DID meanings, values, PIN routines or
writable ECU settings. An entirely separate `VolvoP2CemSupportMatrix`
holds an **empty** production fingerprint catalog.

`VolvoP2CemParameterEvidenceCatalog` is a third, non-operational component
to record **proposed research assertions** about whether a candidate
category might be associated with a specific CEM hardware and software
revision. Its immutable result reports the review category and structural
validation problems; **not vehicle support**.

Every research record contains:
- Original Dvinholm candidate ID, a proposed CEM variant name, exact
  claimed hardware and software revision labels, and a bounded XC90
  model-year interval.
- A separately referenced source note. A human-reviewed claim must also
  supply a referenced study of the display parameter's meaning.
- Review notation: `Proposed` or `HumanReviewedClaim`. Neither proves
  source authenticity, physical ECU identity or parameter decoding.

There are **no bundled production parameter records**. The model uses
synthetic identifiers in tests only. It never reads, stores or emits
vehicle-specific raw values, VINs, credentials, PINs, ECU bytes or
proprietary option IDs.

## Eight automated offline gates

1. Empty production catalog stays unimplemented.
2. A matching proposed research record never enables operations.
3. A human-reviewed notation is still only an unsupported research claim.
4. Unknown candidate IDs and unconfirmed / out-of-advertisement model
   years are rejected.
5. Unknown or conflicting identity and wrong hardware/software revision
   labels do not produce matches.
6. Any malformed record rejects the **entire** catalog: unknown ID,
   invalid year/revision/source, unsupported review notation or missing
   meaning evidence for the human-reviewed notation.
7. Overlapping year intervals for identical candidate / CEM variant /
   hardware / software claims are rejected as ambiguous.
8. The catalog is capped at 64 research records; diagnostic results are
   immutable and do not echo original source notes.

**Critical distinction:** `ReviewedClaimResearchOnly` means only that
submitted strings passed shape checks and claimed notation. It is not
independent evidence verification and grants no permission. Even a
synthetic read-only identity classification with a matching revision
does not authenticate a real ECU.

## What remains before any live functionality

- Obtain independent model-year, actual CEM hardware/software
  fingerprint, exact module ownership, and safely documented transport.
- Validate genuine firmware-specific read-only parameter semantics and
  independent sources on authorized bench hardware.
- Determine verified dependencies, ranges, consent and legal/safety
  limits for every option. Never infer real relationships from vendor
  marketing or structurally valid research references.
- Independently validate real recovery-capable backups, uninterrupted
  power, transport behavior, authorization and readback.
- Review any future executable reader/writer in separate audited
  changes. This catalog has no vehicle service APIs and **cannot**
  authorize PIN acquisition, configuration writes or J2534 messages.

Hardware tests with an actual CEM and installed Windows UI acceptance
remain separate. The existing Stable **v0.2.4** stays unchanged until
the user approves a new release.
