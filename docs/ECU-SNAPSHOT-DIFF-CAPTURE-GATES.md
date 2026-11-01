# Direct ECU snapshot diff: capture metadata validation

Development-only defensive check for the existing read-only DID snapshot
comparison used by the future CEM configuration research workflow. This
does **not** decode configuration, authenticate a physical CEM, contact an
adapter or make an ECU backup restorable.

## Why it is necessary

The imported-historical-file comparison already validates an input's UTC
timestamp and CAN range. But the underlying
`EcuConfigurationSnapshotDiff.Compare` is independently callable on
in-memory snapshots, so bypassing the historical JSON inspector could
previously compare invalid capture metadata and return a seemingly valid
DID difference.

Before comparing any DID value, the direct backend now validates **each**
snapshot independently:

- Non-default UTC capture with **zero offset**.
- Request/response CAN IDs within the declared 11-bit (0..0x7FF)
  or 29-bit (0..0x1FFFFFFF) range, and never identical.
- Non-empty bounded profile ID (up to 120 characters) and profile name
  (up to 160 characters).
- The existing exact same profile identity, CAN addressing, DID set,
  hexadecimal byte and bounded-count checks remain unchanged.

A malformed input rejects the entire comparison before displaying a
result. Eight synthetic tests cover ordinary valid UTC differences,
default/non-UTC times, standard and extended CAN bounds, identical IDs
and invalid profile metadata, checking both comparison directions.

A consistent comparison is still only an **opaque diagnostic DID diff**.
It cannot prove the historical files came from one vehicle, that the ECU
is a Volvo XC90 P2 CEM, that the bytes represent a configuration, that a
PIN is available, or that any configuration may be written.
No Stable v0.2.4 artifact or separate Virtual Car project is modified.
