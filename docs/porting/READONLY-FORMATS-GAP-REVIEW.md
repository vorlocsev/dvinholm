# ReadOnlyFormats -> Dvinholm: offline P2/P3 format review

Source: previously uploaded SectorV-Porting-Research.zip (384495324 bytes),
matching the byte size of the user's Google Drive file. Drive connector
metadata is accessible, but direct download fails at its 256 MiB limit.
The locally uploaded archive passed a full ZIP CRC check; matching file
size is not a cryptographic equality proof of the two ZIP copies.

Source evidence reviewed:
- portable-code/ReadOnlyFormats/DiagnosticFormats.cs
  SHA-256 ed10f2a71f5b3b6eef79778de0fb19d03e054955627dcabd020cd45d4638fe13
- portable-code/Checks/Program.cs
  SHA-256 d097b2925be885fd76119faf1d25cd170e6bb28e41585e627e505056ce2d0aa3
- portable-code/LICENSE (MIT; notice retained in app Assets)
- analysis/CEM_CONFIGURATION.md and evidence/CORRECTIONS.md
- verification/PORTABLE_CODE.md

## Implemented offline body boundary

P2 B9FC: exactly 142 bytes after D2 framing, count 140 (0x8C),
byte 1 checksum = sum(140 values) modulo 256, bytes 2..141 are values.

P3 F106: exactly 254 bytes after positive UDS header 62 F1 06,
count 252 (0xFC), byte 1 checksum = sum(252 values) modulo 256,
bytes 2..253 are values. Full positive UDS PDU = 257 bytes.

CemConfigurationBlockDecoder checks length, count and checksum and returns
an independent byte-array copy. It does NOT select the vehicle, interpret
options through an OEM catalog or make any claim of configuration validity.

The standalone archive also offers strict, RX-only Classic CAN ISO-TP
reassembly. Dvinholm already has its own strict IsoTp and IsoTpReassembler
including length, sequence-number and rollover checks. That code is not
replaced. A synthetic test exercises the existing stack with a full
257-byte positive P3 UDS PDU (one FF plus 36 CF).

The archive notes that one legacy P3 path suppresses a previous repeated
CF sequence number rather than enforcing every expected number. The
standalone reference and Dvinholm are stricter; that behavior is intentional.

No request IDs, CEM access, J2534 session, PIN, write or restore operation
was added. Existing VolvoP2CemConfigurationReadPreflight stays fail-closed.
Format validity does not authorize an actual CEM read. The source archive
reports 6 configuration and 9 ISO-TP synthetic checks; those are source
report claims and do not confirm OEM traces or physical hardware.

New xUnit tests are registered in the native x64 offline pilot. Windows
CI and desktop/physical hardware acceptance remain separate gates.

## Stage: 1-based offline index review and safe comparison

The archive's CEM_CONFIGURATION.md describes P2 1-140 and P3 1-252
indexed configuration values. The P3 decoder is described as matching
exactly ordered 1..252 definitions in an embedded P3 catalog, but the
research document explicitly does not provide the complete hardware
part-number dictionary, firmware applicability or raw option meanings.

Dvinholm's existing VolvoP2CemConfigOptionManifest contains 14
display-only research candidate names (eight vendor advertisements and six
manufacturer menu labels), not verified P2 byte-index definitions. Its
production physical-support and parameter mapping catalogs remain empty.
Ordinal position in those lists MUST NOT be joined to the P2/P3 values.

CemConfigurationIndexedReview converts a validated body to an immutable
index/value list (one-based ordinal and raw byte, with hex presentation)
and SHA-256 of the supplied body. This hash is an integrity fingerprint,
not ECU authentication. The optional Compare routine accepts only the same
P2 or P3 format and reports changed raw ordinal values; it never resolves
names, reads a file or talks to CAN/J2534.

This stage deliberately does not introduce a part-number/firmware dictionary,
auto-variant detection, a service request, an interpreted configuration
editor or any write capability. Physical verification and independently
documented revision-specific option definitions remain blocked.

13 offline xUnit checks are registered for the native x64 pilot: P2/P3
length and index boundaries, unknown raw values, invalid shape, integrity
hash changes, identical/different comparisons, cross-generation rejection,
immutability, research catalog isolation and no-I/O contracts.

## Stage: offline revision-scoped mapping research intake (NOT production)

The SectorV archive analysis/CEM_CONFIGURATION.md establishes that the
P3 definition catalog requires 252 entries ordered by ordinal ID. It
does NOT supply that complete catalog, the hardware-part dictionary or
version-scoped option meanings. The P2 body likewise has 140 raw values
without a reusable verified parameter dictionary in this research package.

CemConfigurationMappingResearch therefore accepts ONLY user-proposed
metadata: P2/P3 format, 1-based ordinal, claimed hardware part number,
claimed software revision, research text and source citation.
The complete bounded (maximum 64) candidate set is validated first.
Duplicate/conflicting exact scopes, invalid indices and malformed input
fail closed. A unique exact match returns only the existing raw value
and status UnverifiedResearchMatch, never a verified option name.
A source citation or self-described revision cannot authenticate a CEM.

The production mapping catalog remains empty. No D2/J2534 connection,
new request path, PIN access, write, configuration backup, firmware
identity validation or model auto-detection is introduced. Existing
VolvoP2CemParameterEvidenceCatalog remains separately scoped to
its P2 XC90 advertising metadata, not to a physical index offset.

10 synthetic xUnit tests cover both formats, missing sources, exact
research-only joins, revision mismatch, invalid/duplicate records,
catalog-wide validation, bounded input, forged snapshots, and no-I/O
capability boundaries. This research gate is not proof of OEM accuracy.


## Stage: opaque SVDB header-only structural preflight

Source: the user-supplied Dvinholm_SVDB_Catalog_Inspection.md; no
original proprietary SVDB bytes are embedded, copied, or committed.

Two user-supplied objects have separate 64-byte SVCAT001 headers,
the observed DWORD 0x00010000 at bytes 8..11, and a value at bytes
12..19 that fits an unencrypted data-length interpretation when
combined with 65536-byte chunks and an additional 16 bytes per chunk.

- catalog.svdb: declared 66,777,157 bytes; 1,019 chunks;
  physical 66,793,525 bytes.
- catalog.001.svdb: declared 13,809,152 bytes; 211 chunks;
  physical 13,812,592 bytes.

SvdbContainerLayoutInspector accepts exactly a 64-byte *supplied*
header and the full file length, checks the known magic/version,
recomputes the size with overflow handling, and returns failure
reasons for malformed inputs. It does not read or dump the encrypted
body, access secret keys, or treat 16-byte per-block overhead as a
cryptographically verified tag. These offsets are observed from
two specimens, not a public OEM standard.

Even a successful structural match cannot authenticate the
container, prove its payload is SQLite, or populate the CEM P2/P3
index-to-meaning dictionary. Previously reviewed VIDA diagnostic
SQLite caches remain separate evidence with different provenance.

Nine synthetic header-only xUnit tests check both observed sizes,
magic/version, block boundaries, zero, truncation, oversized inputs,
and unchanged no-IO capability flags. No physical J2534 run occurred.


## Stage: read a selected local SVDB header only

SvdbLocalFileInspection.InspectFileAsync accepts a user-supplied path ending
in .svdb (case insensitive). It uses a read-only FileStream with shared
readers and asynchronous cancellation, validates a 512-MiB upper file
size, and reads at most 64 header bytes. It passes those bytes and the
full on-disk file length to the existing SvdbContainerLayoutInspector.

The scanner deliberately never allocates a buffer for the encrypted body,
hashes or extracts that body, copies source catalogs into the repository,
installs a provider, opens SQLite, or derives a CEM configuration mapping.
A short/truncated input returns a structured failure. The source path,
non-header bytes and user-specific data are not included in the result.
No UI integration or automatic file discovery is introduced: a caller
must explicitly select the local path.

10 synthetic offline xUnit tests cover valid tiny/multiple-block files,
unchanged input bytes, short and empty input, invalid magic, physical
size mismatch, extension gating, uppercase extension, cancellation,
size bounds and a source-level no-I/O capability contract. These tests
do NOT open the user's original SVDB files or prove cryptographic
authenticity. Physical J2534 and VIDA verification remain separate.


## Stage: strict offline import of research-only JSON claims

New parser CemConfigurationResearchCatalogImport accepts only explicitly
selected local UTF-8 .json files up to 32 KiB. The JSON root has exactly
six fields: format, schemaVersion, isUnverifiedResearch,
isRestorableBackup, mayAccessEcu, claims. Required header values are
"dvinholm-cem-index-mapping-research", 1, true, false and false.

Each claim has exactly six fields: dataFormat ("P2B9Fc" or "P3F106"),
index1Based (1..140 or 1..252), hardwarePartNumber (6..20 numeric
characters), softwareRevision (2..64 allowed ASCII characters),
claimedMeaning (3..100 characters with controls/angle brackets rejected),
and evidenceReference (12..256 characters with controls rejected).
The existing CemConfigurationMappingResearch.Validate routine checks
every claim, including duplicate/conflicting exact scopes. JSON duplicate
properties, unknown fields (including VIN/PIN/service credentials),
incorrect types, trailing content, unexpected flags, empty catalogs and
more than 64 claims are all rejected. UTF-8 BOM and malformed UTF-8 are
not accepted. Neither the JSON file nor private source text is committed.

The result contains a read-only detached set of unverified claims and
fixed false capability flags. Consumers may explicitly pass it to the
existing research assessor for unverified exact-string metadata matches.
The redacted review reports only counts for P2/P3; it omits part
numbers, firmware strings, meanings, references and local paths.
No implicit merge into the empty production mapping catalog, driver
installation, SVDB decryption, ECU request, PIN access or write exists.

Example of a *synthetic* local research file (not an OEM mapping):
```json
{
  "format": "dvinholm-cem-index-mapping-research",
  "schemaVersion": 1,
  "isUnverifiedResearch": true,
  "isRestorableBackup": false,
  "mayAccessEcu": false,
  "claims": [{
    "dataFormat": "P2B9Fc",
    "index1Based": 1,
    "hardwarePartNumber": "30682981",
    "softwareRevision": "SW-A",
    "claimedMeaning": "Synthetic unverified option",
    "evidenceReference": "Private reference from a synthetic test"
  }]
}
```

13 synthetic test cases are registered in the x64 offline pilot.
Passing them does not prove an OEM mapping or authorize live access.


## Stage: WinUI 3 CEM research catalog viewer (offline only)

The approved Aurora CEM configuration page now has a compact
"Local P2/P3 JSON (offline)" action below the research filters. This is
a separate UI workflow: it does NOT replace the existing vendor/OEM
reference list, enable the existing Read/Compare/Write buttons, alter
a vehicle profile or introduce a seventh workspace.

A user explicitly selects one validated research JSON. A modal viewer
shows 1..64 unverified claims, their source text on selection, and an
All/P2/P3 filter (the current UI language controls labels and warnings).
It may separately request one user-selected .bin file. Only an exact
142-byte P2 B9FC or 254-byte P3 F106 body with a valid count and
checksum is parsed. Positive UDS service headers and framing must
already have been removed. A P2 body is NEVER joined to P3 claims
or vice versa; only one-based ordinal raw bytes are juxtaposed.
There is no inference that a claimed part number matches a car.

Both file pickers operate outside a live ContentDialog. The modal list
does not persist JSON, binaries or the user's sources. No diagnostic
request, CEM PIN, write, recovery or OEM catalog promotion is added.
A chosen file's raw value is explicitly labeled local and unverified.
All user-supplied names/references are displayed as plain text,
without executing them, and are not written to logs or repository.

10 synthetic unit/contract tests cover the view filter, generation
isolation, raw byte values, invalid/forged metadata, immutable lists,
the exact binary body file gate and WinUI XAML integration. Windows
rendered UX, DPI/viewport and RU/EN screenshots still need manual
Aurora capture to establish actual visual acceptance.


## Stage: RU/EN and long-claim visibility hardening

The research-only modal now uses a wrapping DataTemplate rather than a
single-line DisplayMemberPath. Full user-supplied meanings are visible in
the scrollable research list, and selected hardware/software/evidence
details remain selectable in a separate bounded detail scroll region.
The list is 176 DIP tall and the detail region 142 DIP maximum to reduce
clipping within the existing 430-DIP modal at the approved 1280x800
client size. These dimensions are implementation constraints, not
evidence of a successful physical screenshot capture.

Each row's unverified disclaimer is localized in RU or EN, both when
no binary body is selected and when a matching offline raw byte is
present. The original unverified claim text is displayed unchanged;
neither localization nor matching the P2/P3 ordinal authenticates it.
Five new theory/contract cases protect the locale and wrapping behavior.

Actual visible GUI acceptance at user DPI, scrolling, keyboard focus,
and source-text readability still requires interactive Windows capture.


## Private exact-head CEM research viewer screenshot script

The viewer now publishes stable, non-sensitive UI Automation IDs for
the modal, P2/P3 filter, research list, safety warning, and selected
details. The additional Windows PowerShell 5.1 helper
eng/Capture-CemResearchViewerWinUI3.ps1 is SYNTAX-CHECKED ONLY in CI.
It must never execute there or upload/commit screenshot data.

For private visual acceptance, manually launch the exact-head WinUI 3
pilot from a full artifact containing Aurora-x64-build-evidence.json,
manually select RU or EN, open the CEM research modal using synthetic
local JSON, and inspect either its list or a selected synthetic claim.
Run the helper on the actual logged-in Windows desktop. It requires
an explicit no-secret confirmation, validates running EXE SHA256 and
native window title against artifact provenance, checks 1280x800 DIP
client dimensions, Per-Monitor V2 and requested DPI (192 by default,
corresponding to 200% Windows scaling), live modal UIA anchors and
foreground visibility. Output must go in a new empty folder outside
the repository. One PNG and a private SHA256 manifest are produced.

A 1280x800 DIP client at 200% scaling needs at least 2560x1600
physical pixels PLUS native caption. The helper refuses a clipped
window rather than mislabelling an image as accepted.

Example operator commands (use separate folders per view and locale):

~~~powershell
$sha = '<EXACT_HEAD_40_HEX>'
.\eng\Capture-CemResearchViewerWinUI3.ps1 -Language ru -View list -ExpectedDpi 192 -CommitSha $sha -OutputDirectory 'C:\Dvinholm-CemReview\ru-list' -IConfirmSyntheticNoSecrets
.\eng\Capture-CemResearchViewerWinUI3.ps1 -Language ru -View details -ExpectedDpi 192 -CommitSha $sha -OutputDirectory 'C:\Dvinholm-CemReview\ru-details' -IConfirmSyntheticNoSecrets
~~~

Repeat using -Language en after manually switching the app language.
The script does not open pickers, choose files, click controls, connect
to an adapter, or validate a real CEM configuration. Actual visible GUI
screenshots and human examination of long links, scrolling and 200%
DPI are still required; a passing CI is NOT visual acceptance.


## Shipped synthetic review fixtures

The x64 pilot review artifact now includes four deliberately fake inputs:
- eng/fixtures/CemResearch-Visual-Synthetic.json
- eng/fixtures/CemResearch-Visual-Synthetic-P2.bin (142 bytes)
- eng/fixtures/CemResearch-Visual-Synthetic-P3.bin (254 bytes)
- eng/fixtures/CemResearch-Visual-Synthetic-Corrupt-P2.bin (wrong checksum)

The JSON has three P2/P3 research claims with claimed hardware
part 00000000, software revision SYNTHETIC-1, source references only
under the reserved example.invalid domain, and multilingual/long
test descriptions. It is NOT a Volvo option mapping. The binary
bodies contain zero-filled synthetic values except for two chosen
endpoints and a valid additive checksum. No VIN, PIN, real DID or
proprietary catalog bytes are included. All four files are tested
by the offline x64 suite; the intentionally corrupt one must fail.

Manual UI procedure for each language: open the synthetic JSON
in the CEM research viewer, capture the list, select a long claim
and capture the details, optionally pick the synthetic P2 or P3
body and confirm generation isolation. The corrupt P2 body must
show a checksum error without closing the application.

The operator screenshot helper MUST still run on a real Windows
desktop with the exact-head artifact, not in CI. Synthetic
fixtures enable repeatable review; they do not constitute actual
captured screenshots or human visual acceptance.

## Screenshot feedback: Aurora styling for CEM Research modal

A user-provided real Windows capture from exact-head x64 pilot #732
shows a readable RU list at 96 DPI, 1280x800 DIP: three synthetic
claims, wrapped text, visible warning/filter and footer. This screenshot
does not establish visual acceptance at 192 DPI, details selection or
English, and was not copied to the repository.

Review identified a visual mismatch: the research modal used the
default charcoal ContentDialog background and a strong accent on the
Close button. The dialog now reuses AuroraPanelGradient and
AuroraCardEdgeGradient from the existing approved six-page shell,
with a 1-DIP border and 12-DIP corner radius. The default button is
None to avoid highlighting Close as the primary action.

This is a styling-only adjustment. No diagnostic or configuration
capability changes. Manual recapture is required after the change to
confirm whether the actual WinUI ContentDialog template honors the
surface brushes and whether 96/192 DPI, RU/EN remain legible.


## Interactive screenshot regression: selected details clipped (#733, RU, 96 DPI)

A user-supplied exact-head pilot #733 screenshot with the first
synthetic P2 record selected showed its "Claimed meaning" text
cut off at the bottom of the visible region. The 142-DIP nested
details scroller and the parent modal scroll made the source reference
inconvenient to inspect. This is a real visual-review failure despite
green synthetic/CI tests. The received screenshot is NOT copied into Git.

The viewer now has an 820-DIP modal and a side-by-side research
area with equally sized columns. A 290-DIP list remains visible at
left, and a 290-DIP separately scrollable, selectable details region
at right, with explicit localized section labels. The outer
430-DIP scroll wrapper and old 142-DIP details viewport are removed.
The modal remains within the approved fixed 1280x800 DIP client.

A source-level regression test guards the two-column layout, fixed
width/height values and absence of the legacy nested viewport.
This is NOT yet confirmed by a new live screenshot. Recapture RU
details, then verify English and both generation filters. GUI capture
and OEM/physical CEM acceptance are separate.


## Stage: two-file P2/P3 raw ordinal comparison (backend only)

CemConfigurationOfflineComparison.CompareFilesAsync accepts two explicitly
selected local .bin paths, validated using the existing 142-byte P2 B9FC
or 254-byte P3 F106 file inspector. Each body must pass its count,
length and additive checksum. Mixed P2/P3 generations are rejected.
No diagnostic frames, ECU profiles, VINs, part numbers or research
catalog descriptions are used to align the data.

The detached result includes the generation, total ordinal count,
number of changed ordinals, ordered one-based changed indices, and
in-memory SHA-256 fingerprints of the two already-validated bodies.
These are equality/integrity hints only, NOT signatures, provenance,
CEM firmware identification, option mappings or recovery data.

CreateRedactedReport(english) returns an RU or EN summary containing
only generation, counts, changed one-based ordinals and explicit
"not verified / not a restorable backup" warnings. It omits local
paths, hashes, raw before/after values, claimed option meanings and
source references; no file is written. The method cannot initiate
any physical connection or persist private configuration bytes.

11 synthetic tests cover unchanged P2, first/last P2 differences,
index 252 P3, mixed-format failure, invalid checksum, malformed
files/extensions, cancellation, input immutability, RU/EN redaction,
immutable result and strict no-I/O/no-PIN contracts.

This is intentionally a backend-only stage. The user deferred WinUI
visual acceptance: no new GUI button, window-layout change, visual
capture requirement or hardware J2534 acceptance is introduced.


## Stage: offline changed-ordinal versus claimed CEM revision triage

CemConfigurationRevisionResearchTriage.Assess takes the already validated
in-memory result of two locally compared P2/P3 bodies, a strict
research-only JSON catalog and an *unverified user-asserted* hardware
part number/software revision. The part/revision fields are validated
using the same metadata constraints as the original research intake
and never imply a VIN or a physical CEM identity.

For each changed one-based ordinal, only a matching P2/P3 format
is considered. The disposition is:
- ExactUnverifiedUserClaim: same ordinal, claimed part and claimed
  software revision (still NOT an authenticated option or ECU).
- OnlyOtherSoftwareRevisionClaim: same part/ordinal but different
  claimed software revision.
- OnlyOtherHardwarePartClaim: another claimed hardware part for
  that ordinal, with no record for the asserted part.
- NoClaimForChangedOrdinal: no record for the format/ordinal.
- AssertedScopeMissingOrInvalid: missing or malformed user claims.

The entire catalog must validate first, not just the changed rows.
A comparison is also structurally checked for exact 140/252 count,
uppercase SHA fingerprints, strictly ascending one-based ordinals
and consistency between changed-count and equal/different hashes.
These checks are anti-confusion metadata controls, NOT cryptographic
authentication or proof of the two files' real-world origin.

The immutable output keeps only changed ordinals and categories, not
part numbers, software versions, option descriptions, raw bytes or
source links. The RU/EN report explicitly redacts such values and
states that live access, PIN and configuration writes are forbidden.
It does not add a GUI action, persist data, modify Stable/Virtual Car
or request any physical J2534 operation. Visual acceptance is deferred.

13 synthetic automated test methods cover exact/missing/other scope,
both generations, claims priority, empty diff, invalid catalog,
forged comparison, ordinal boundaries, RU/EN redaction and no-I/O
capability assertions.


## Stage: executable three-file offline CEM research review pipeline

CemConfigurationOfflineResearchPipeline.ReviewFilesAsync combines the
existing strict file-level components into one ordered, cancellation-
aware and bounded application backend operation:

1. Explicitly supplied local JSON: parse as strict unverified
   research metadata (32 KiB maximum, schema v1, at most 64 claims).
   Any missing, malformed, conflicting or unsafe claim data fails
   before binary comparison is started.
2. Two separately supplied local .bin bodies: independently decode
   exactly P2 B9FC 142-byte or P3 F106 254-byte formats, enforcing
   count/checksum and rejecting mixed P2/P3 generations.
3. Compare raw one-based ordinals using the existing index diff,
   then triage changed positions against only user-asserted part
   numbers, software revisions and source claims.
4. Return the existing immutable, redacted-by-default
   CemConfigurationRevisionResearchAssessment. Its RU/EN report
   excludes paths, raw before/after bytes, private source content,
   hashes, part numbers and revision strings; no report file is
   persisted automatically.

The flow has no implicit file discovery, OEM identity, VIN, CAN
frames, diagnostic requests, PIN operation, live read, write or
restore method. Even an "exact" metadata match stays explicitly
unverified. This is backend only: WinUI visual acceptance remains
deferred and the screen layout is untouched.

13 synthetic xUnit checks cover complete P2/P3 workflows, changed
first/last indices, mismatched part/software/generation,
equal files, invalid JSON/duplicate source scopes, corrupt
checksum, mixed generation, cancellation, no content leakage
in RU/EN and original file immutability. No physical J2534 test.


## Stage: redacted claim-disposition statistics per changed P2/P3 ordinal

The offline revision triage result now also exposes
CemConfigurationRevisionResearchStatistics with five mutually
exclusive counts taken from the *changed raw one-based ordinal* rows:
exact user-claimed scope (always UNVERIFIED), other claimed software
revision, other claimed hardware part, no research claim at that ordinal,
and a missing/invalid asserted part or software revision.

"Incompatible claimed scopes" is the sum of the two revision/part
mismatch counts. It is NOT a Volvo/OEM compatibility finding.
The five category counts must sum to the number of changed ordinals.
Several research JSON claims for the same ordinal contribute at most
one row, according to the existing exact > other software > other
hardware > missing precedence. With an invalid asserted part/revision,
all changed ordinals are labelled invalid scope, not "missing in catalog".
An empty diff produces all-zero counts, not a false hardware match.

RU and EN CreateRedactedReport now include these numerical aggregates
before the ordinal-by-ordinal dispositions, preserving the existing
warnings and redaction of VINs, local paths, hashes, raw byte values,
part numbers, firmware strings, claims and evidence references.
Unknown enum statuses throw before a report can be issued.

10 synthetic tests cover each category, empty diff, duplicate-claim
deduplication, status priority, 140 changed P2 positions, invalid
asserted scopes, immutable snapshots, RU/EN report privacy and
absence of any diagnostic or write/export calls. The implementation
does not touch WinUI, Stable, Virtual Car, physical J2534 or OEM data.
The user has deferred visual interface acceptance.


## Stage: bounded P2/P3 research-catalog coverage by claimed scope

CemConfigurationResearchCoverage.Assess operates solely on an explicitly
provided strict, research-only JSON catalog and a user-asserted hardware
part/software revision. The entire catalog is validated before analysis.
It enumerates all 140 P2 B9FC or 252 P3 F106 *raw one-based ordinals*,
not just positions changed between snapshots.

Every ordinal enters exactly one of four buckets: exact user-asserted
part/revision scope (always UNVERIFIED), same part but other claimed
software revisions, only other claimed hardware parts, or no research
entry for this format/index. Counts sum to 140 or 252. The percentage
shown is the **portion of raw indices with an exact user claim**, not
verified OEM coverage or functional support. Multiple research records
for the same index do not inflate these counts.

Separately, "potential cross-source text differences" flags ordinals
within the selected hardware part that have both distinct source
references AND distinct claimed meaning strings across the available
software revisions. Different revisions may legitimately describe
different functions. These are *text differences needing investigation*,
not proven semantic conflicts, OEM defects or evidence of an ECU
configuration change. The strict importer already rejects competing
records for the exact same format/index/part/revision scope, and those
rejected duplicates MUST NOT appear as detected conflicts.

The immutable result and RU/EN redacted report retain only ordinal
numbers, counts, percentages and research-only labels. Original source
text, URLs, part/revision strings, VIN, paths, hashes, raw bytes and
CEM configuration contents are not disclosed in the report. No network,
file output, diagnostic adapter, live ECU read, PIN or write operation
is introduced. WinUI visual acceptance remains deferred. Stable and
Virtual Car remain separate and untouched.

13 synthetic test methods cover both generations, all four exclusive
categories, precedence with multiple claims, cross-revision text-only
flags, negative flag cases, strict rejection of malformed/duplicate
scopes, immutable outputs, RU/EN privacy and no hardware/write APIs.


## Stage: whole-format catalog coverage vs actually changed P2/P3 positions

CemConfigurationResearchChangeCoverage combines the two existing
strict, unverified research assessments for the SAME format and
claimed hardware part/software revision. Its source checks each
changed ordinal against the full 140/252-index coverage and rejects
a mismatch of generation, ordered ordinals, totals, status or claimed
scope rather than reporting misleading counts.

For privacy and coherent provenance, the new
CemConfigurationOfflineResearchPipeline.ReviewCoverageAndChangesAsync
inspects the user-selected JSON catalog ONCE, compares TWO explicitly
selected validated local .bin bodies ONCE and derives both assessments
from these same inputs. JSON validation is completed before reading
either binary body. The prior ReviewFilesAsync API remains unchanged
and uses the same shared file-inspection sequence.

The new redacted RU/EN result contrasts:
- exact unverified claim coverage over the entire 140/252 raw indices;
- exact unverified user claims among ONLY changed raw indices;
- changed positions whose claims are for another asserted software
  revision or hardware part, and positions without any research claims;
- unchanged exact-claimed or no-claim positions (by subtraction);
- changed ordinals lacking an exact asserted scope;
- only those possible cross-revision source-text flags that fall on
  actually changed positions.

Percentages explicitly distinguish full-format coverage from coverage
among changes and handle zero changes without dividing by zero. This
is NOT verified OEM mapping, physical CEM identity, PIN access, read,
write or restorable backup. The report contains no source text, part
number, claimed software version, VIN, paths, hashes or raw body bytes.
The workflow does not introduce new UI actions; visual acceptance
remains deferred.

13 synthetic tests cover P2/P3 boundary indices, zero/full changes,
full-vs-changed missing-claim counts, scope inconsistency, source-text
intersection, malformed changes, safe RU/EN redaction, and the whole
three-file pipeline (including unsafe catalog rejection). Stable,
Virtual Car, physical J2534 and release publishing are unaffected.


## Stage: independent CEM diagnostic evidence checkpoint (offline only)

The combined P2/P3 comparison/catalog-coverage result now has a
companion CemConfigurationOfflineEvidenceChecklist. It intentionally
keeps seven independent proof gates OPEN in ALL cases, including
valid 142/254-byte bodies, exact claimed-revision matches, an observed
experimental part-number response, or a locally satisfied 8-item
session prerequisite snapshot.

The seven required independent gates are:
- original provenance of both local binary bodies and research records;
- verified same-physical-module association among those sources;
- authenticated physical hardware, application and diagnostic SW;
- verified OEM raw-index semantics for the exact generation/revision;
- documented revision-scoped transport, read-only services and errors;
- authorized bench/adapter/timing acceptance;
- verified service permission, rollback and recovery procedure.

ReviewEvidenceChecklistAsync reuses the strict three-file pipeline
(first research JSON validation, then both P2/P3 checksummed bodies,
then one shared revision-scope coverage/change analysis). It returns
a redacted RU/EN evidence checkpoint, not ECU credentials, an
authenticated module signature, configuration backup or a command plan.
Optional existing CemIdentityEvidenceAssessment and
CemReadOnlyPrerequisiteAssessment are accepted only through the pure
offline Assessor and are explicitly marked UNCORRELATED. Their known
statuses are validated using the existing fail-closed formatters;
even a "connected" or "observed" status cannot link a local .bin to
an ECU, authenticate firmware or close any of the seven gates.

The report contains only raw format, positions/counts, safe boolean
context and generic missing-evidence labels. It omits part numbers,
revisions, PIN, VIN, file paths, hashes, source text, payloads, CAN IDs,
transport parameters and ECU diagnostic responses. No network request,
adapter connection, vehicle read, file write, PIN path or backup
capability is introduced. There is no WinUI visual acceptance or
physical J2534 claim.

11 synthetic xUnit checks cover both generations, changed/unchanged
bodies, identity guard revocation, apparently satisfied session
prerequisites, invalid/forged external evidence assessments, redacted
RU/EN output, exact three-file operation and no live/write APIs.


## Stage: local three-source provenance claims and bounded SHA-256 audit

The CEM offline research pipeline now has a separate
ReviewProvenanceAsync method for an explicitly user-selected previous
P2/P3 .bin, current same-generation .bin and research .json, plus
exactly three role-tagged user-authored provenance declarations.

Each declaration carries an UNVERIFIED source label, user-claimed
module-association tag, asserted hardware/SW strings, UTC observation
time and uppercase declared SHA-256 fingerprint. Duplicate/missing
roles, malformed digests/labels, invalid revision strings and
non-UTC/out-of-range years fail before any local file inspection.
The submitted original/updated ordering is a user assertion only.

The local workflow fingerprints the bounded JSON before and after
strict catalog import (max 32 KiB), and rereads the two 142/254-byte
bodies after the previously validated offline comparison. Any
change in byte content between these independent reads makes the
audit fail closed. SHA-256 matches between declared and actual
local content are reported independently for all three sources;
a declared mismatch is visible, not silently accepted as a verified
provenance statement.

The RU/EN report includes ONLY three local hash-match statuses,
counts, boolean agreement of user-claimed module labels and HW/SW
strings, plausibility of claimed observation chronology, and the
existing seven OPEN physical/OEM evidence gates. It deliberately
withholds actual SHA hashes, source labels, timestamps, paths, raw
bytes, original claims, asserted revisions, VIN and any vehicle
configuration content.

An exact SHA-256 can check local BYTE CONSISTENCY with an entered
reference. It cannot authenticate capture time, source, legitimate
ownership, OEM semantics, physical CEM identity, or that different
sources describe the same vehicle/module. No OEM gates are closed,
no PIN/capability is enabled, and no J2534/UDS requests, writes,
backup exports or WinUI changes are introduced.

11 synthetic tests cover digest matches/mismatches, duplicate roles,
unsafe metadata, UTC timestamps, module and revision disagreement,
reversed chronology, RU/EN redaction, three-file end-to-end checking,
early rejection and absence of diagnostic/write calls.


## Stage: bounded offline CEM evidence links and repeat-inspection journal

CemOfflineEvidenceLedger is an **in-memory, unverified research ledger**
built from the existing strict three-source provenance declarations and
the three-file offline provenance audit result. It does not import
a service database, send a diagnostic request or identify a vehicle.
Only three bounded role types (previous P2/P3 body, current body,
research JSON) are supported. An optional set of up to 12 explicitly
user-declared links can assert same module label, same claimed HW/SW
strings or distinct source labels. Each link is evaluated only as
user-text agreement, text conflict, identical source-label warning or
distinct but still unattested source labels. Reversed duplicate links,
self-links and unknown roles/types fail closed.

The ledger also holds up to 64 repeat-check observations in memory.
Each row names a source role, **caller-claimed UTC** and a locally
computed or separately user-provided SHA-256. Historical entries must
be monotonically ordered per source role with no duplicate timestamp.
The journal counts matches against that source's originally declared
digest, differences between successive recheck digests and rechecks
with claimed timestamps earlier than the original claimed observation.
None of these statuses attest time, independent source ownership,
firmware revision or actual same-ECU provenance.

CemOfflineEvidenceLedger.CaptureLocalRecheckAsync uses the same
strict 142/254-byte P2/P3 decoder or 32-KiB JSON catalog importer,
plus a second bounded SHA-256 read to detect changed bytes between
validation and fingerprint. It returns only an in-memory event,
never a persisted log or raw data. ReviewEvidenceLedgerAsync plugs
the complete existing three-file provenance inspection into this
ledger without changing the earlier APIs.

The RU/EN redacted report provides link findings and a role-specific,
ordered recheck journal but omits original source labels, claimed
part numbers/revisions, timestamps, source references, file paths,
raw hashes, VIN and configuration values. **All seven independent
physical/OEM evidence gates remain OPEN**, even with all three
local fingerprints matching and all declared source labels differing.
The ledger is not a backup, VIN/PIN proof, authoritative OEM map or
vehicle read/write capability.

18 synthetic xUnit test methods check unchanged/contradictory user
claims, identical vs different source labels, duplicate/self/unknown
links, multiple rechecks and digest drift, claimed-time chronology,
bounds, RU/EN redaction, locally revalidated P2 and JSON files,
end-to-end three-file pipeline and absence of diagnostic/write APIs.
No UI change or physical J2534 acceptance is included; visual
acceptance is still deferred.


## Stage: caller-claimed recheck freshness and explicit redacted text export

CemOfflineEvidenceFreshness is a deterministic, backend-only cross-check
of the existing in-memory evidence ledger, three local SHA-256 status
results, each role's most recent declared recheck and an EXPLICIT
caller-claimed UTC audit date. Its configured acceptable age is
bounded to 1 hour..30 days. This is a **user policy**, not an
authenticated time source or trusted capture timestamp.

Every source role is classified into one of seven exclusive states:
initially mismatching local/declared fingerprint, not rechecked,
claimed recheck before original claimed observation, claimed recheck
in the claimed audit date's future, latest recheck digest mismatch,
latest recheck older than the selected policy, or locally consistent
but explicitly UNVERIFIED. Prior digest drift, text-link conflicts,
shared claimed source names and chronology warnings remain visible.
No missing history or locally matching hash automatically closes
any physical/OEM evidence gate; all seven remain OPEN.

The full three-file ReviewFreshnessAsync pipeline reuses strict JSON
and body decoding before assessing the supplied in-memory links and
journal. A separate CemOfflineEvidenceRedactedExport offers
PrepareUtf8 and SaveNewTextFileAsync for an explicitly selected,
fully qualified NEW .txt output path. The export is strictly
UTF-8 without BOM, at most 32 KiB, and uses FileMode.CreateNew
(no overwrite); pre-cancellation performs no write. It exports
ONLY the existing redacted RU/EN report: no raw CEM bytes, SHA
values, original source IDs, VIN, HW/SW claims, file paths or
timestamps. An attempted export never writes to any vehicle.

All freshness statuses depend on UNTRUSTED declared times, even if
the SHA-256 is computed from local bytes. Physical CEM identity,
source independence, protected diagnostic services, PIN and writes
remain disabled. UI visual acceptance remains deferred; no changes
to Stable, Virtual Car or release publication.
