# XC90 P2/CEM: manual VIDA identity worksheet (offline)

Development 0.3.0. This is an **offline entry form for user-observed
part-number text**, not a CEM protocol implementation and not a vehicle
compatibility grant.

## Source basis and limits

The historical Volvo diagnostic service documentation reproduced in the
[2007 XC90 CEM diagnostic functions chapter](https://lemon-manuals.org.ua/Volvo/2007/XC90%20FWD%20L6-3.2L%20VIN%2098%20B6324S/Repair%20and%20Diagnosis/Body%20and%20Frame/Body%20Control%20Systems/Description%20and%20Operation/Central%20Electronic%20Module%20%28CEM%29/Diagnostic%20Functions/)
lists hardware part number, hardware serial number, software part
number and diagnostic software part number as control-module identity
data read by VIDA. This reference is a *third-party republication*
of workshop material, not a documented public diagnostic service
or an authenticated individual vehicle reading.

The official [Volvo DiCE/J2534 guidance](https://www.volvospecialtools.com/index.cfm?event=info.dice)
recommends DiCE for legacy D2/GGD communication and explicitly
distinguishes J2534-1's software-download support from diagnostic
support. A detected generic J2534 DLL does not establish a valid
XC90 P2/CEM transport. VIDA's exact CEM request/response addresses,
identifying DIDs, encodings and service security mechanisms are **not
provided by these references** and must not be guessed.

## How to use

1. Obtain the XC90 model year and three part numbers from an
   **authorized** VIDA control-module identification display, when
   accessible. The form does **not** connect to the vehicle; it
   does not require an adapter or access credential.
2. In Dvinholm Development, select **P2 configuration candidates
   (offline)**, then **Enter CEM identity (offline)**.
3. Enter the claimed model year (2002–2014), CEM hardware P/N,
   CEM software P/N and CEM diagnostic software P/N. Optionally
   tick the box saying that you personally copied the values from
   VIDA. This is **self-report**, not independent verification.
4. Review the resulting local worksheet. Only the explicit
   **Copy worksheet** action sends text to the OS clipboard.
   Cancel and Close never copy or persist anything to disk.

For privacy, **do not enter** the hardware serial number, VIN,
immobilizer PIN, credentials, vehicle owner details or raw ECU
response bytes. The Dvinholm form does not request or store those
fields. Part numbers themselves may be fingerprinting information;
review the text before sharing. Clipboard persistence is controlled
by the operating system.

## Strict preflight

The worksheet requires all three part numbers and a year within
the first-generation XC90 research range. Part-number strings have
bounded length, safe ASCII characters and no whitespace/control
characters. A contiguous 17-character alphanumeric string is
rejected as potentially VIN-shaped. The application never
interprets these part-number strings as hexadecimal, CAN IDs,
DID codes, memory offsets, CEM PINs or software packages.

Eight synthetic xUnit tests enforce complete fields, year
scope/boundaries, invalid characters/sizes, VIN-like rejection,
report normalization and sensitive field exclusion, unverified
source attribution and permanent denial of live ECU/PIN/write
permissions.

**Evidence distinction:** A complete worksheet is a manually
entered claim, not an authenticated CEM identity, verified firmware
compatibility or a real configuration backup. All live ECU, PIN,
configuration-edit and write flags are **false**. The production
CEM identification and parameter mapping catalogs remain empty.

Do not enable real CEM reads or security functions until a
specific vehicle/firmware family, supported transport and safe
identification commands are independently documented and
validated. Installed Windows UI, virtual adapter and physical
vehicle acceptance are tracked separately. Stable v0.2.4 is
unchanged and no release is authorized by this research.
