# Passive CAN RX: possible ISO-TP PCI shape summary (Development 0.3.0)

This extends the **RX summary** dialog in CAN Monitor with bounded analysis
of the *already retained* Classic CAN frames, independent from the software
ID filter. It is a transport research aid for the XC90 P2/CEM project,
**not** a CEM detector or a working ISO-TP decoder.

## Evidence and scope

- Volvo's [Workshop System Requirements 320EN20](https://workshopsupportguide.volvocars.biz/media/35358/VIDA-WS-Requirements-320EN20-en-GB.pdf)
  recommends **DiCE** for D2/GGD communication. It specifically excludes
  **J2534-1 from VIDA diagnostics**; J2534-2 software downloads are not
  evidence of CEM diagnostic compatibility.
- Public workshop references mention different logical VIDA CEM node labels
  for earlier and later P2 cars, but a VIDA logical node is **not** an
  independently verified CAN request/response arbitration ID.
- No publicly checked source in this task verifies an exact diagnostic
  transport and service mapping for the user's physical CEM revision.

## Operation

1. Retain received frames in the existing CAN Monitor, or use the **synthetic
   development** data for UI exercises. Ensure bus speed and adapter safety
   independently before connecting; this feature never starts a monitor.
2. Press **RX summary**. The existing ID/DLC summary remains intact.
   A second text section counts potential normal-addressed Classic CAN
   ISO-TP PCI shapes grouped by 11/29-bit CAN arbitration ID.
3. Review the bounded per-ID counts: SF (single), FF (first), CF
   (consecutive), FC (flow-control), or other. Only the first byte and a
   minimal frame-length check are used to classify shapes; the whole
   canonical frame payload is validated for consistency.
4. The existing **Copy RX metadata** button copies both text sections
   only after an explicit click. The **Copy JSON metadata** button continues
   to copy the old ID/DLC-only JSON schema unchanged.

This is a **heuristic**. Unrelated CAN messages can look like an ISO-TP PCI
frame. Extended addressing, K-line, proprietary Volvo D2, reassembly,
transport timing, flow-control state, request/response pairing and identity
matching are **not** inferred. A frame in an RX monitor is not proof that the
bus was passive, that another tool didn't transmit or that it came from CEM.
The newest 5,000 frames may be incomplete due to eviction or queue drops.

Privacy: the classifier reads buffered bytes in memory solely to count shapes.
The text never contains raw payloads, full DIDs, VIN, PIN, timestamps, profile
metadata or local file paths. CAN IDs and frame counts can still fingerprint
vehicles, so review before sharing. No automatic persistence, clipboard copy,
network request or J2534 transmission occurs.

Eight offline tests cover empty, all four possible PCI shapes, 11/29-bit
separation, invalid-pattern rejection, malformed buffers, privacy redaction,
bounded groups and a virtual J2534 RX-only integration asserting zero TX.
Physical DiCE/CEM validation and installed WinUI acceptance remain deferred.
Stable v0.2.4 and the separate Virtual Car project are unchanged.
