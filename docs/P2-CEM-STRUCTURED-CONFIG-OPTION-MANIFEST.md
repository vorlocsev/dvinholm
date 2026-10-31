# XC90 P2: structured configuration option manifest (Development)

The initial editor-facing schema is `VolvoP2CemConfigOptionManifest`. It
normalizes **14 display-only research labels** into one immutable collection:
eight source-attributed SectorV-advertised P2 categories and six separate
Volvo XC90 CEM service-menu headings (Technical Journal 20278.6.11).

## Source separation and field meanings

| Field | Current state | Meaning |
| --- | --- | --- |
| CanonicalId | Existing Dvinholm-owned research ID | Does not reuse proprietary vendor/factory parameter IDs |
| Provenance | ThirdPartyAdvertisement or VolvoPublishedCemServiceMenu | A manufacturer menu heading is not a validated CAN parameter |
| EvidenceReference | Respective upstream reference | A source reference is not a vehicle identity |
| ClaimedModelYearFrom / Through | 2006–2014 for advertised P2 category; null for OEM menu | A claim, **not** an applicability allowlist |
| AdditionalSafetyOrEquipmentReviewRequired | Existing candidate review/conditional equipment marker | Not a completed safety assessment |
| VerifiedPhysicalModuleOwner | null | The manufacturer menu location does not establish byte ownership |
| VerifiedCemHardwareRevision, VerifiedCemSoftwareRevision | null | No physical CEM identity has been confirmed |
| VerifiedRawValueMapping | null | No CAN IDs, DIDs, memory offsets or encodings |
| VerifiedAllowedValues, VerifiedDependencies | immutable empty lists | **Unknown**, not unrestricted or no dependencies |
| Read / edit / PIN / transmit flags | false | No CEM configuration actions authorized |

The existing offline **CEM candidate finder** now reads this manifest
instead of building a second independent 8+6 list. Its report exposes missing
physical owner, firmware, byte mapping, values and dependencies. Research
search still supports the same source filter, exact canonical ID and bounded
free-text query.

This is a *schema prerequisite* for a future configuration editor, not the
editor itself, an authenticated configuration inventory or a restorable backup.
An unverified year and menu label cannot grant vehicle access. No J2534,
ISO-TP, UDS, PIN, write, firmware or recovery operation was added.

## Eight automated checks

1. Exactly 14 distinct IDs.
2. Vendor categories preserve original IDs, category and evidence.
3. Vendor date scope stays advertising only.
4. Manufacturer CEM menu rows retain manufacturer provenance and unknown years.
5. No raw mapping, physical owner, allowed values or executable ability.
6. Manifest and nested collections are immutable.
7. Exact case-sensitive lookup rejects invalid/oversized identifiers.
8. Existing finder renders the unified manifest and remains completely offline.

The tests run within the existing Windows x86 Development CI suite.
Installed WinUI and real DiCE/J2534/CEM verification remain separate, deferred
acceptance gates. The published Stable v0.2.4 is unchanged.
