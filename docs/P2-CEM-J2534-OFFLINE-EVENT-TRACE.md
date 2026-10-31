# Offline J2534 event trace inspection for Volvo P2 CEM research

The CAN Monitor **Inspect trace** button accepts two distinct local formats:

1. Dvinholm's `.csv`: Classic CAN RX frames, with heuristic ISO-TP
   and D2-looking envelope summaries, including unverified sequence counts.
2. **Metadata-only J2534 `.jsonl`** with `schema: 15` events: an offline
   temporal analysis of PassThruConnect, PassThruWriteMsgs, PassThruReadMsgs
   and PassThruDisconnect **calls**. It does NOT decode CAN payloads.

This second format was studied from the independent
[Virtual Car J2534 tracing source](https://github.com/vorlocsev/virtualcar/blob/c20f1870d125ebca8683c97a11b507910824fb0f/proxy/j2534_trace_proxy.c)
and its [trace attribution tool](https://github.com/vorlocsev/virtualcar/blob/c20f1870d125ebca8683c97a11b507910824fb0f/trace_attribution/main.go).
**No Virtual Car repository, build, release or workflow is modified.**
No original `VirtualCarJ2534.dll` or proprietary binaries are included.

## Parser safety and boundaries

The JSONL reader accepts 1..5000 metadata event lines, at most 512 KiB,
maximum 1024 characters per line, UTF-8 without BOM, `schema = 15`,
bounded integral numeric fields, canonical `0xXXXXXXXX` CAN-ID strings,
and known metadata keys only. It deliberately rejects any extra fields
such as raw payload, VIN, secrets, paths, or duplicate JSON properties.
A malformed event aborts the entire inspection without producing a partial
trusted summary. Files are opened read-only; no copy or sidecar is saved.

Only a fixed textual report plus bounded aggregate integers is displayed.
CAN IDs, J2534 channel/device IDs, timestamp values, raw class strings,
filenames and paths are never included. Copying remains an **explicit**
button action. The user's file can itself contain sensitive metadata and
should be reviewed before sharing; aggregate counts can also fingerprint a
vehicle or test scenario.

## Evidence interpretation

- `connect` opens a tentative channel generation; a repeated successful
  connect resets the previous observation window.
- A `write` with `status = 0` and `count > 0` opens a temporal window.
  An unsuccessful write invalidates a prior window on that channel.
- A subsequent successful `read` with `count > 0` on that same channel
  is counted as **temporally following an accepted write**, not as an ECU
  response. Concurrent calls, multiple messages per batch and lossy
  tracing prevent reliable reply attribution.
- A successful disconnect, new connect or backwards 32-bit timestamp
  invalidates pending temporal assumptions. Wraps are treated as uncertain
  boundaries; no clock continuity or timestamp unit is inferred.
- Count selected source-labelled candidate categories A3 / B9FB / B9E0 /
  B9F0 / B9FC on accepted writes and E3-02 / F9E0 / F9F0 / fragments
  on successful read batches. These are **user-editable labels**, not
  verified Volvo D2 frames, ECU identity, response bytes or PIN access.
- Older logs, unknown metadata fields or alternative J2534 trace dialects
  are intentionally rejected rather than guessed.

No CSV frames are constructed from JSONL because the proxy does not record
the required raw CAN data. The two offline formats are kept separate.

## Acceptance scope

Eight offline/synthetic xUnit checks cover successful TX/read windows,
read-before-write and failed writes, channel separation and reuse,
timestamp resets, strict rejection of arbitrary/sensitive metadata,
bounds, actual local UTF-8 file inspection and UI wiring.

**Not covered:** real Volvo DiCE/J2534 hardware, Windows installed UI
acceptance, physical XC90 model year/CEM revision, diagnostic transport,
CEM request/response ID, verified B9FB format, VIN decoding, PIN recovery
and vehicle configuration read/write. None of those operations is unlocked
by this research tool.
