# Aurora: separate workspaces and shared navigation

The main window now follows the Aurora concept throughout the application:
one persistent sidebar, a selected section, dark controls/dialogs, consistent
page headings, blue actions, bordered cards and vertically scrollable content.
The old single diagnostic surface is replaced by seven distinct destinations.

| Destination | Content |
| --- | --- |
| CEM PIN | P/N identification, bounded search, encrypted vault, offline readiness review |
| CEM configuration | Offline catalogue and explicit action to open its existing detail dialog |
| ECU blocks | Passive discovery, response observations and selection |
| Diagnostics | Verified TX/RX addressing, DID/VIN/DTC reads and explicit results |
| CAN monitor | Capture start/stop, filters, trace tools and frame table |
| ECU profiles | Search, editing, import/export and diagnostic snapshots |
| Connection | J2534 driver selection, channel parameters and local adapter details |

Home cards, quick actions and sidebar items use the same destination model.
Navigation itself never opens a driver, sends a frame, reads a PIN or opens a
research dialog. Those operations still require their existing explicit action.
All existing named diagnostic controls and service handlers are retained once,
with the same session and dispatcher. The configuration/readiness detail dialogs
remain available through buttons on their corresponding pages.

Changing sections cancels a pending private diagnostic operation and clears
its presentation context. PIN expiry, encrypted persistence and ECU access gates
remain intact. Navigation does not disconnect an adapter or stop an explicitly
started CAN monitor. Connection state is shown in a shared header button; local
operation status remains in the footer. Development demo tools stay on their
relevant pages. Experimental PIN cards are hidden in a Stable feature build.

The sidebar stays visible on every page and can scroll in short windows. Page
content scrolls vertically. Diagnostic actions and PIN fields use two-column
rows; profile actions retain their existing flexible grid. Home retains its
4/2/1 priority-card layout at window widths >=1280, 900–1279 and <900 respectively.
The original owned banner is also packaged as lossless PNG for native Windows
decoding, without relying on optional WebP codec installation. The hero displays
the original brand artwork rather than overlaying a second large wordmark.

Eight new structural/navigation tests check unique destinations, sidebar routes,
control ownership, invalid route rejection, shared status and dark theme. Existing
home, private result, profile layout and protocol checks remain applicable.
Windows GUI acceptance and CI results are recorded in the implementation PR.
No new vehicle protocols, ECU writes, key programming or physical support claims
are introduced. Published Stable v0.2.4 and Virtual Car releases remain frozen.

## Compact main-window shell (Development)

The persistent *main-window* navigation now has its own two-width adaptive
layout, independent from the landing dashboard's four/two/one card layout.
At window widths below 980 effective XAML pixels it keeps all seven routes in a
64-pixel icon rail, collapses only their visible captions and decorative
brand/footer blocks, and keeps full names available through tooltips and UI
Automation. The Home action continues to navigate back; no navigation command
opens J2534 or sends ECU traffic.

In that compact state the workspace header moves the connection/cancel actions
onto a second row, reduces heading size and outer gutters, and keeps the
workspace and sidebar vertically scrollable. At 980 pixels and above the
original 216-pixel labeled sidebar and wide header remain unchanged. Layout
setters affect presentation only and do not alter active CAN monitoring,
existing session state, read results or PIN expiry rules.

Eight offline XAML contract checks verify breakpoints, compact rail, all seven
labels, accessibility affordances, header reflow, scrolling/gutters, valid
targets and preservation of explicit vehicle-operation handlers. These checks
are **not** a substitute for real Windows GUI resizing at 980, 800, 640 and
smallest supported window sizes; that manual test remains open, as does
physical DiCE/J2534 acceptance. No new Stable release or Virtual Car change.
