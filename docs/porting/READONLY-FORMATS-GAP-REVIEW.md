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
