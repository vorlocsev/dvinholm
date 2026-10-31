# XC90 P2: Volvo-documented CEM service-menu names

**Status:** manufacturer-document-backed menu names, **not** verified
firmware parameter bytes, a vehicle-compatible diagnostic profile or
working read/write access.

## Primary manufacturer evidence

Volvo Cars **Technical Journal 20278.6.11**, dated 2016-09-06,
distributed through the US NHTSA database:

https://static.nhtsa.gov/odi/tsbs/2016/SB-10087303-5448.pdf

PDF page **16 of 17** (XC90 page 3) lists **six XC90 CEM service
setting categories**, accessible in the VIDA vehicle communication
advanced service menu:

| Volvo CEM menu category (English summary) | Dvinholm research ID |
| --- | --- |
| Home safe lighting | `xc90.oem.cem.homesafe-lighting` |
| Daytime running lamps | `xc90.oem.cem.daytime-running-lamps` |
| Tailgate wiper (five-door model) | `xc90.oem.cem.tailgate-wiper` |
| Fuel filler cover locking delay | `xc90.oem.cem.fuel-filler-lock-delay` |
| Bi-Xenon low-beam position warning | `xc90.oem.cem.bixenon-lowbeam-position-warning` |
| Three-flash turn indicator | `xc90.oem.cem.three-flash-indicator` |

The document lists these as **service settings**, not universal
capabilities of every individual XC90 or factory-supported values for
all model years. The **`ResearchId` strings are new Dvinholm
names**, not Volvo diagnostic identifiers, DID addresses, programming
nodes, numeric offsets or protocol bytes.

The original eight `VolvoP2ConfigOptionCatalog` labels, sourced
from third-party vendor advertising, remain **separate**. Do not
silently merge the two sets or upgrade a vendor-only claim based
on a similar-sounding manufacturer menu item.

The manufacturer PDF does **not** disclose ECU firmware hardware/software
revision applicability, current values, allowed raw values, bit
positions, checksums, CEM request/response CAN IDs, UDS DID numbers,
PINs, service authorization or executable read/write instructions.
The source documents a menu **at service-workflow level only**.

### Interface compatibility must also be verified

Volvo's own [DiCE/J2534 information](https://www.volvospecialtools.com/index.cfm?event=info.dice)
makes a material distinction:

- DiCE is recommended for VIDA diagnosis of older D2/GGD-based cars.
- Volvo describes **J2534-1 as not usable for VIDA diagnostics**; the
  manufacturer's documented use there is emissions-related software
  download over high-speed CAN on qualifying MY2004+ vehicles.
- J2534-2 expands software download coverage to additional networks,
  but its presence alone does **not** certify a given pass-thru driver,
  XC90 CEM diagnostic session, programming permission or an arbitrary
  aftermarket application.

Therefore discovery of a generic J2534 DLL or successful connection
does not mean Dvinholm can safely talk to CEM. Model year, network,
protocol, genuine interface support and expected responses still need
independent evidence and physical acceptance.

## Developer gate

`VolvoXc90OemCemMenuCatalog` supplies six immutable, non-operational
manufacturer menu labels. All flags remain **false** for
variant/firmware compatibility, read and write mappings,
live ECU requests, editing and authorized PIN functions.
The existing offline configuration catalog viewer displays the
manufacturer-sourced names in a distinct section, with the source
and a prominent unsupported limitation.

Eight deterministic xUnit tests cover exact six-menu enumeration,
source provenance, optional equipment notes, permission denial,
unknown research identifiers, no proprietary/raw parameter IDs,
immutable separate evidence catalogs and clear offline UI display.

**No physical J2534 or CEM tested; no installed Windows UI acceptance.
No PIN extraction, actual configuration reading or writes implemented.**
No new Stable release without explicit approval.
