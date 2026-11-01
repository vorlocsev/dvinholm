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
