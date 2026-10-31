# P2/CEM: offline candidate Volvo D2 RX envelope shapes (Development)

The existing CAN Monitor **RX summary** now contains an additional
payload-redacted, **read-only** section. It classifies only a few
proprietary-looking byte arrangements in **already retained** Classic CAN RX
frames. It does not enable or use Volvo D2 as a Dvinholm diagnostic protocol.

## Research source and limits

The separate Virtual Car project includes a [passive J2534 raw-message
classifier](https://github.com/vorlocsev/virtualcar/blob/c20f1870d125ebca8683c97a11b507910824fb0f/proxy/p2_message_classify.c).
It recognizes candidate E3, F9 and fragment-shaped RX headers in **J2534
messages with a four-byte CAN-ID prefix**. Dvinholm's CAN Monitor history
already contains **only the CAN payload**, so this feature inspects that
payload directly and does NOT reinterpret it as a four-byte J2534 prefix.
Conceptual header patterns are adapted conservatively; no Virtual Car source
file, original diagnostic executable or vehicle data is imported.

The optional candidate labels are:
- E3-02-like: `CB/CF 40/50 E3 02` in the first four payload bytes.
- Other E3-like: `CB/CF 40/50 E3`, excluding E3-02.
- F9-E0-like and F9-F0-like: `CB/CF 40/50 F9 E0/F0`.
- Broad fragment-like initial byte: `08..0F`, `40..4F`, `80..8F`.

**These are not validated Volvo D2 frames.** The four header bytes are not
proof of a response, ECU address, CEM revision, compatible bus speed, valid
service, accepted request/response pairing, or physical vehicle. The broad
fragment byte ranges overlap unrelated traffic and are particularly prone
to false positives. Unknown/truncated/other patterns remain unclassified.

The display shows counts by 11-bit/29-bit CAN format, not actual CAN IDs.
The existing ISO-TP and ID/DLC summaries remain unchanged, as does the
strict existing ID/DLC-only JSON metadata schema. All data is processed
in memory. Only explicit **Copy RX metadata** copies the text to clipboard.

## Safety and validation

The reporter reuses the existing bounded 5000-frame history, validates
canonical CAN IDs and exact DLC/uppercase-hex payload, and distinguishes
history eviction, subscriber drops and unknown counter state. No raw frame
data, timestamp, VIN, DID, PIN, CAN ID, source path or profile label enters
the D2 subsection. Counts can still fingerprint traffic characteristics;
review before sharing.

Eight synthetic/offline tests cover empty history, E3-02 candidate routes,
F9 and other E3 variants, broad false-positive fragment shapes, 11/29-bit
separation, malformed frame rejection, history loss/synthetic provenance,
and virtual J2534 RX with zero transmitted frames plus UI binding.

Physical J2534/DiCE, proprietary D2 protocol authentication and an
installed Windows UI test are **not** claimed. No requests, CAN writes,
adapter setup, PIN access or CEM configuration read/write are enabled.
Virtual Car is **not modified**, and its releases stay independent.
