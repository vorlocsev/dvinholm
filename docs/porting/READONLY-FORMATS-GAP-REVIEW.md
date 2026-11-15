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
