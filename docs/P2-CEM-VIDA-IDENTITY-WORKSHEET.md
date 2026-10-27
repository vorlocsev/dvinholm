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
4. Review the resulting local worksheet. **Copy worksheet** places
   the summary on the OS clipboard. **Save research JSON...** opens
   the existing path-only Windows Save As dialog; choose a writable
   location outside the application directory to export a small,
   versioned JSON research record. Cancel and Close never write
   or copy anything. Neither action connects to a vehicle.

### Open a previously saved identity research JSON

From **P2 configuration candidates (offline)** select **Open CEM
research JSON...**, choose your own previously saved `.json` file,
then review its contents in a **read-only, clearly unverified** dialog.
Opening is local and never changes the active ECU profile, CAN speed,
adapter connection, configuration or stored application data. Cancel
creates no file or state change.

The reader imposes the same strict 4 KiB size limit, UTF-8 (without BOM),
exact schema and safety flags as the exporter. It rejects malformed,
unexpected, duplicate or dangerous properties, invalid strings,
invalid text encoding and a claimed ability to access an ECU or restore
a configuration. No file name or private path appears in status text.
A saved document is a user-editable claim, not a proof of VIDA origin,
CEM identity, actual hardware compatibility, PIN entitlement or
a safe programming procedure. Do not put VIN/PIN/serial/secret data in
a file intended for this inspector.



### Compare two saved CEM identity research cards

In Development, select **Open CEM research JSON...** and review the
first research card. Choose **Compare another JSON...**, then explicitly
select the second saved card. The app uses the same bounded strict UTF-8,
versioned schema and unverified-only checks for each file. The resulting
read-only comparison marks **SAME** or **DIFFERENT** for the claimed model
year, CEM hardware P/N, CEM software P/N and CEM diagnostic software P/N,
showing both entered values. The user-claimed VIDA origin flag is
compared separately and **never** counts as proof or a hardware match.

Matching numbers cannot prove the files came from the same physical CEM,
and differing numbers do not prove incompatibility. The two saved
files are untrusted, editable notes, not a verified module inventory or
firmware support table. No filename, local path, serial, VIN, PIN or
raw ECU data is included; the visible part numbers themselves can be
identifying metadata. Cancelling the second picker leaves both files
unchanged. Nothing is saved automatically, and no profile, ECU session,
PIN or configuration operation is invoked.

Eight synthetic comparison tests check identical records, each of four
identity-field differences, a differing source claim, and fail-closed
behavior for unsafe or noncanonical records. Installed Windows picker
behavior is **not** proven by these unit tests and requires a separate
manual acceptance check.


For privacy, **do not enter** the hardware serial number, VIN,
immobilizer PIN, credentials, vehicle owner details or raw ECU
response bytes. The Dvinholm form does not request or store those
fields. Part numbers themselves may be fingerprinting information;
review the text before sharing. Clipboard persistence is controlled
by the operating system. The JSON may also identify the car's
electronics; you are responsible for where you save or send it.

## Strict preflight

The worksheet requires all three part numbers and a year within
the first-generation XC90 research range. Part-number strings have
bounded length, safe ASCII characters and no whitespace/control
characters. A contiguous 17-character alphanumeric string is
rejected as potentially VIN-shaped.

The optional JSON file has fixed schema v1, exact known fields and
hard size limit **4 KiB**. It explicitly records that the content
is **unverified**, **non-restorable** and **cannot authorize ECU
access**. The backend research inspector rejects duplicate/unknown
fields, invalid type/format, contradictory flags, dangerous text
and out-of-range model years. It revalidates the same three part
numbers as the live worksheet. A JSON file can always be edited
and does **not** authenticate a CEM or VIDA session.

Saves reuse Dvinholm's verified atomic report writer and reject
destinations inside the application directory, which the updater
may replace. A failed save does not truncate an existing report.
A cancelled path-only picker creates no file. No autosave, no
telemetry or stored VIN/PIN. The imported-file inspection is a
backend parser for subsequent research tools, **not** a new ECU
profile import or a live capability grant. The application never
interprets these part-number strings as hexadecimal, CAN IDs,
DID codes, memory offsets, CEM PINs or software packages.

Eight worksheet xUnit tests enforce complete fields, year
scope/boundaries, invalid characters/sizes, VIN-like rejection,
report normalization and sensitive field exclusion, unverified
source attribution and permanent denial of live ECU/PIN/write
permissions. A separate **eight-test JSON export suite** covers
canonical roundtrips, sensitive-data exclusion, duplicate/extra
fields, unsafe capability flags, malformed data, bounded size,
opt-in user-selected UTF-8 writing and preventing invalid/app-folder
exports from replacing existing files.

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
