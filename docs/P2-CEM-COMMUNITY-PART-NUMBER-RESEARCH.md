# P2 CEM hardware part-number research cross-reference (Development 0.3.0)

This is a **small, non-exhaustive, third-party research index** in the
existing *manual VIDA CEM identity worksheet*. It never authenticates a car,
detects a physical ECU or configures a diagnostic adapter.

## Source and attribution

Source: [vtl/volvo-cem-cracker, pinned revision
`0130cd055da075823dd15a98e4a11d5496128e60`](https://github.com/vtl/volvo-cem-cracker/blob/0130cd055da075823dd15a98e4a11d5496128e60/volvo-cem-cracker.ino),
community-maintained GPL-3.0 project. The small subset of numerical
hardware-part-number **mentions** below is cited as factual source metadata
only. No project code, security-access implementation, PIN-search sequence,
key material, bus speeds, CAN arbitration IDs or service commands are copied
or used.

The referenced project *advertises* Volvo P2 and first-generation XC90
coverage, but does not independently establish that any individual part
number belongs to a particular XC90 model year/engine, is installed in a
specific vehicle, or offers a supported Dvinholm diagnostic route.

## Selection and ambiguity

| Source-reported P2 CEM category | Selected P/N mentions |
| --- | --- |
| CEM-B (community label) | 8645716, 8688434 |
| CEM-L (community label) | 30682981, 30786889, **30682982** |
| CEM-H (community label) | 30728539, 30786890, **30682982** |

**30682982 appears in both CEM-L and CEM-H source sections.** It must be
reported as **CATEGORY AMBIGUOUS**; the app may not select a physical revision
from it. Neither a single-category mention nor a manually entered model year
is proof of a physical CEM or supported protocol.

The app intentionally indexes only this *selected subset*. A number absent
from the subset means **not in the selected research sample**, never
unsupported, incompatible or invalid hardware. Only a canonical 7-8 digit
string is queried; free-form notes, whitespace, URLs, VIN-shaped text and
invalid identifiers are not interpreted or echoed in the note.

## User experience

Open the existing XC90 P2 CEM manual VIDA identity worksheet, type the
claimed model year plus hardware, application and diagnostic-software part
numbers, and choose **Review notes**. After the worksheet has validated all
fields, it appends a short, fixed-text cross-reference for the hardware P/N.
The source note neither changes the source-claim flag nor upgrades any
verification state. The existing optional **Copy worksheet** / **Save
research JSON** interactions remain explicit and unchanged. Research JSON
schema version 1 remains untouched, with `isUnverifiedManualClaim=true`,
`mayAccessEcu=false`, and `isRestorableBackup=false`. Importing the JSON
shows the same research note but is never evidence of vehicle capture.

The note does not echo extra P/N digits or sensitive information. The
worksheet itself already shows the three numbers that the user chose to
enter; **review before sharing**.

## Safety and tests

- A community mention is not Volvo documentation, XC90 model-year/engine
  fitment, supported firmware, diagnostic addressing, verified J2534/K-line
  transport, permission to read configuration, or PIN authorization.
- No bus speed is inferred or applied. The app never opens a vehicle
  session, transmits CAN, performs PIN search or changes the configuration.
- Eight offline checks cover the three source categories, the duplicate
  L/H number, an absent but valid number, malformed/private input,
  unavailable executable capabilities, existing worksheet integration,
  and unchanged saved research JSON.
- Installed Windows UI acceptance and physical DiCE/J2534/CEM verification
  remain outstanding. The catalog of *production-verified CEM DID
  signatures* stays empty. Stable v0.2.4 and Virtual Car remain unchanged.

This adds a research breadcrumb, **not** compatibility support.
