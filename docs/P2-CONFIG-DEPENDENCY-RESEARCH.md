# Dvinholm Development 0.3.0: P2 configuration dependency research preflight

Status: **offline candidate research only; all operational capabilities denied**.

## Purpose

The independent Dvinholm `VolvoP2ConfigOptionCatalog` lists eight high-level
XC90 P2 display categories based on vendor-advertised functionality. These are
**not** Volvo firmware option IDs, real values, verified parameter locations,
supported CEM functions, or editable vehicle settings.

`VolvoP2ConfigDependencyResearch` allows future, externally researched
*propositions* about relationships between these categories to be inspected
structurally without converting them into a vehicle configuration plan.

### Supported proposition shapes

- `Requires`: a research claim that one candidate may depend on another.
- `PotentialConflict`: a research claim that two categories may interact
  incompatibly (undirected; reverse duplicate rejected).
- `ReviewTogether`: categories requiring shared human research review.

**No actual dependency relationship is bundled as validated production data.**
The caller must provide untrusted candidate relationships; absence of evidence
is reported as `NoResearchClaims`, not assumed compatibility.

## Fail-closed checks

- Both endpoints must be distinct, exact, case-sensitive entries from the
  independently named eight-candidate catalog.
- Only known relation kinds are accepted.
- Duplicates, self-links and cycles formed by `Requires` are rejected.
- Source notes must be present, bounded to 256 characters, and free from
  control characters. They are unverified research references, never proof.
- At most 64 claims are accepted; validation of lazy/infinite inputs is bounded.
- A year must be explicitly confirmed within the candidate catalog's
  **vendor-advertised** 2006–2014 range. This does not certify actual CEM or
  option compatibility. Earlier XC90 years are outside that advertising scope,
  not proven incapable.
- Invalid evidence cannot produce a partial plan or partial allowed set.
- Assessment messages contain generic issue codes and no sensitive input
  source notes or ECU data.

**All assessment results permanently deny** vehicle requests, configuration
reads, edit preparation, service authentication/PIN finding and configuration
writes. Source notes, model-year claims and structural graph validity cannot
override these denials. No ECU mapping, raw bytes, DID, CAN frame, security
method, VIN, adapter access, backup, rollback or write command is introduced.

## Validation stages remaining

1. Collect independent service documentation or lawfully obtained bench
   evidence for exact XC90 P2 model-year, ECU hardware and software revisions.
2. Confirm the owning ECU and parameter semantics separately for each option.
3. Identify real dependencies, allowed values, compliance constraints and
   provenance without copying undocumented proprietary encodings.
4. Design and independently review a read-only reader with explicit consent.
5. Validate authentic scoped authorization and backup/recovery in a separate
   future stage. Never infer service rights from a PIN checklist.
6. Test power stability, J2534 transport, recoverability and revision matching
   on bench hardware with an authorized vehicle owner.
7. Keep config writes gated behind a distinct, explicit implementation, safety
   review, and user approval; this module cannot enable them.

## Automated checks

Eight deterministic xUnit tests verify empty graphs; correctly shaped but
non-operational propositions; unknown options and years; null, self and
unknown references; invalid relation IDs, duplicates and reverse conflicts;
requirement cycles; source bounds; and immutable redacted diagnostics. Tests
are source-only and require no physical J2534 adapter or ECU.

CI, installed Windows UI checks and actual hardware acceptance are separate.
Stable v0.2.4 stays unchanged. No Stable publication without the user's
explicit confirmation.
