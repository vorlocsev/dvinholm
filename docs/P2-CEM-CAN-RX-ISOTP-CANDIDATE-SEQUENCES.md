# Passive ISO-TP candidate sequence research (Development 0.3.0)

**Purpose:** help study historical CAN Monitor RX captures for a *possible*
normal-addressed Classic CAN ISO-TP sequence pattern. This does **not**
identify a Volvo P2/CEM, prove ECU addressing, decode Volvo D2 or authorize
any diagnostic/security/configuration operation.

## What the implementation does

1. Uses up to 5,000 already buffered CAN Monitor RX rows, independent of
   the current software display filter. Never connects to an adapter or
   transmits any frame.
2. Shares the existing strict canonical byte parser and CAN metadata
   validation. Rejects malformed input rather than displaying it.
3. Groups by the exact arbitration ID **and** 11/29-bit frame format.
   A valid-looking eight-byte first frame (FF) supplies a 12-bit length
   greater than seven and six presumed initial bytes.
4. Tracks possible consecutive frames (CF) only **on that same ID**,
   requiring four-bit sequence numbers 1, 2, ..., 15, 0, 1, ...
   until the announced byte length could be reached. Count matching
   patterns as *plausible completion*, never as a verified exchange.
5. Tracks interruptions, sequence mismatches, unpaired CF and candidates
   that remain unfinished at the end of the retained buffer. Frames on
   different IDs can interleave without implying transport pairing.
6. Displays at most 64 sorted groups. Source is explicitly labelled
   synthetic versus unverified RX; eviction and queue-drop evidence is
   described conservatively.
7. Appends redacted candidate statistics to the existing **RX summary**
   and its explicit text clipboard copy. The existing JSON remains
   **ID/DLC-only** with the same schema and no payload-derived fields.
8. Eight synthetic/virtual J2534 tests cover completion, interleaving,
   SN wrapping, gaps, interruptions, incomplete capture, invalid data,
   bounds, redaction and zero transmissions.

## Limitations

- **Heuristic, not a decoder.** Normal addressing is assumed for the
  candidate counts. Extended-addressed ISO-TP, 29-bit address extension,
  K-line, proprietary Volvo D2 and other transport layers are NOT
  interpreted. An unrelated CAN frame can accidentally look like FF or CF.
- **No request/response identification.** A candidate FF and its CF are
  tracked on one CAN identifier for research statistics only. Neither a
  tester's CAN ID nor the complementary flow-control address is guessed.
- **No completeness guarantee.** Old frames may have been evicted; other
  frames can be missed beyond the tracked subscriber drops. A plausible
  completion does not establish a correctly reconstructed message.
- **No data disclosure.** Bytes are validated only in memory. Reports
  never reproduce payloads, VIN, PIN, DID values, timestamps or ECU
  profile metadata. CAN IDs and traffic counts still can fingerprint
  vehicles, so copy only after explicit review.
- **No CEM revision identity.** A real physical CEM firmware/hardware
  fingerprint and authenticated transport still require independent
  documentation, controlled physical DiCE/J2534 tests and user authorization.
- Stable v0.2.4 and the separate Virtual Car project remain unchanged.

This feature supplements existing static ID/DLC and PCI-shape reports
without modifying normal CAN Monitor, UDS, driver handling, or the launcher.
