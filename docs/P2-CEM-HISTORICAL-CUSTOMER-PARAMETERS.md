# XC90 P2 CEM: historical customer parameter value research

Status: **Development 0.3.0, offline source research only.**
No real CEM identity, current parameter readback, memory layout, PIN,
security access, recoverable backup or configuration write is implemented.

## Source and deliberately narrow scope

The page [Reading off and programming data, XC90 I 2002-2006](
https://diagnostdata.com/volvo/xc90/i-2002-2006/remont/miscellaneous-electrical-design-and-function/reading-off-and-programming-data/reading-off-and-programming-data/)
is an **unverified third-party republication of historical workshop text**.
Its value lists motivate offline input review for **MY2003-2006 only**.
They do not establish that any physical XC90 in this period supports
those values in the same CEM revision, or any raw service commands.

The source contains an internal scope discrepancy: one portion enumerates
seat heating, approach lights, post-drying and daytime lamps, while another
refers to *three* CEM customer parameters. Dvinholm **does not** resolve
this by guessing a firmware mapping or silently removing a category.
The model names are research-only UI labels, not Volvo parameter IDs.

| Research-only customer parameter | Historical visible values and cautions |
| --- | --- |
| Heated seat temperature | 0, 10, 28, 30-41 (except 29 and 42), and 43 degrees Celsius; two LOW/HIGH levels per seat, HIGH must exceed LOW |
| Approach lighting | 0, 30, 60, 90 seconds; 30 seconds described as a historical default, **not a measured actual value** |
| Post-drying | ON or OFF; not confirmed for an individual vehicle or firmware |
| Daytime running lights | Three historical Flex modes: ZERO, STANDARD, LOW; local road-lighting laws/equipment may prohibit certain choices |

The code's `VolvoP2CemHistoricalCustomerValuePreflight` requires an
**independently confirmed model year** for a scope-limited comparison.
It checks only canonical strings and a seat-heater LOW/HIGH relationship.
It returns `ListedInHistoricalSourceOnly` rather than a vehicle support,
readiness, configuration validity or write authorization verdict.
Dates later than MY2006 are **outside this particular source**, not
known unsupported by Volvo.

## Distinct data categories that must not be mixed

Historical [CEM system overview](
https://lemon-manuals.org.ua/Volvo/2007/XC90%20FWD%20L6-3.2L%20VIN%2098%20B6324S/Repair%20and%20Diagnosis/Body%20and%20Frame/Body%20Control%20Systems/Description%20and%20Operation/Central%20Electronic%20Module%20%28CEM%29/System%20Overview/)
describes a **vehicle-configuration file** in CEM with VIN,
structure-week and equipment metadata; this is not the same as
customer-adjustable parameter values.

A separate [historical Volvo software-products chapter](
https://lemon-manuals.org.ua/Volvo/2007/XC90%20FWD%20L6-3.2L%20VIN%2098%20B6324S/Repair%20and%20Diagnosis/Testing%20and%20Inspection/Programming%20and%20Relearning/Software%20Downloading/Software%20Products/)
describes adding/removing vehicle functionality through ordered
software products and changes to the CEM configuration file.
That is a distinct authorized software workflow, **not evidence
that individual bytes should be patched directly**.

The historical customer-parameter reproduction also describes
manufacturer-ordered replacement software and CEM/immobilizer
credentials arriving through the Volvo central database. This
is not a documented independent PIN retrieval algorithm or permission
for Dvinholm to perform security access.

## In-memory proposed-value draft preview (Development)

Open **P2 configuration candidates (offline)**. The top of the existing
dialog provides a temporary research form: manually enter the *claimed*
model year, affirm that you checked the year independently (this is still
a self-report, not authenticated vehicle metadata), choose one historical
customer-parameter label, and enter its proposed canonical value.
For heated seats, specify both LOW and HIGH temperatures.

Click **Review proposed value (offline, no save)**. The local preview
uses `VolvoP2CemHistoricalCustomerValuePreflight` to check exact
historical source notation and the LOW/HIGH dependency. A valid-looking
proposal remains `ListedInHistoricalSourceOnly`, never an actual ECU
value, configuration plan, identified firmware mapping or write
permission. Missing/invalid/out-of-scope year and values fail closed,
without echoing arbitrary untrusted input. Even with the year checkbox,
the information remains user claimed. Cancel/Close discards everything;
no file is saved, no ECU profile is modified and **no J2534/UDS, PIN or
vehicle read/write action** occurs.

The 8 synthetic tests check accepted historical seat, lighting,
post-drying and Flex mode inputs, rejection of reversed/incomplete
temperature pairs, absent/unconfirmed/wrong years, invalid values and
redaction of accidentally entered private strings. WinUI behaviour
on an installed Windows system must be manually accepted separately.

### Compare an optional manual note with the proposal (still offline)

In the same **P2 configuration candidates (offline)** research form, optionally
fill **MANUAL reference notation (NOT an ECU read)**. Heated-seat proposals
also allow a separate manually entered reference HIGH temperature. If the
manual reference is omitted, the existing proposal preview works as before.
If any reference text is entered, **Preview / compare manual notes** evaluates
both sides independently against the same exact-year historical source rules.

Only two complete canonical research notations can yield **SameManualNotation**
or **DifferentManualNotation**. Both outcomes describe a difference between
**two user-typed strings**, not a detected current vehicle setting, verified
CEM configuration, confirmed ECU mapping or effective write. If the year is
unchecked, outside the original documented scope, a temperature pair is
inconsistent, or either notation is not listed, the comparison is blocked
and both raw inputs are withheld from the report. The year checkbox is a
self-report, not proof of vehicle identity. Editing any field invalidates
the previous preview; switching parameter clears both proposals and manual
references rather than carrying values between unrelated categories.

Eight deterministic offline tests check matching/differing claims, correct
LOW/HIGH comparisons, absent/invalid/sensitive notes, invalid proposals,
unconfirmed/out-of-scope model years, extra second fields and all false
live-ECU/PIN/write permissions. **No file, profile, snapshot, current value,
PIN, programming command or write plan is created or updated.** This is
deliberately not a before/after vehicle configuration comparison. Installed
WinUI acceptance remains a separate manual check.

## Eight offline tests and release boundaries

The synthetic checks cover exact immutable source value sets,
unconfirmed/out-of-period years, valid seat LOW/HIGH pairs, invalid
seat relationships, valid/invalid lighting durations, post-drying
notation, all three DRL modes with legal warning, and permanent
denial of CEM/PIN/write operations even for acceptable text.

The **P2 configuration candidates (offline)** viewer displays this
reference without connecting to an adapter, identifying a real CEM,
reading a vehicle, storing desired settings, creating a write-plan
or changing a profile. Future user-facing editing needs separately
verified hardware/software part numbers, exact transport/mapping,
readback, legal compliance, uninterrupted power/recovery and
authorized physical testing. Release Stable v0.2.4 is untouched.
