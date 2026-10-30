# XC90 P2 / CEM offline candidate finder (Development)

The existing **P2 configuration candidates (offline only)** dialog now starts
with an instant-search index rather than showing the long research text by
default. It lists **14 research labels** from the existing bounded data:

- **8** Dvinholm-named *vendor-advertised* P2 candidate categories
  (advertised P2 MY2006–2014, not individually verified on any XC90).
- **6** separately documented Volvo XC90 CEM service-menu names from
  TJ 20278.6.11, XC90 page 3, which do not have verified
  model-year/firmware-specific read or write mappings.

Users may search by label, Dvinholm research ID or category, with
case-insensitive matching of all whitespace-separated terms. An explicit
**evidence tier** selector shows all, vendor advertising, or manufacturer
service-menu entries without blending the sources into a supported option
or CEM command. Zero matches are identified as an empty research result,
not an unsupported vehicle verdict.

The original detailed offline overview and source notes remain accessible
under **Show complete historical CEM research notes (offline)**, collapsed by
default. The other historical customer-value previews, legacy transport
review, and manually selected CEM revision JSON workflow are unchanged.

## Boundaries and acceptance

Search is in-memory only, length-limited to 80 characters, never logged
or echoed, and cannot open any J2534/CAN session. Do not enter a VIN, PIN,
credential or private value. Entries are immutable; their current vehicle
values, module owners, configuration mappings, PIN services, backups, and
read/write capabilities all remain **UNKNOWN or DISABLED**.

Eight synthetic tests cover all 14 IDs and source tiers, empty searches,
case-insensitivity, tier separation, multi-token matching, input bounds
and report privacy, immutable fail-closed results, and main-window UI
wiring. The full Windows GUI is not manually accepted by these tests.

VirtualCar and any J2534 mock integration are left untouched.
Stable v0.2.4 is unchanged. No release publication is requested.
