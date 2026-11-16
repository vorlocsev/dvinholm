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
