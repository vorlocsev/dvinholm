# Dvinholm Aurora | Approved four-screen UI reference
Status: **USER APPROVED, 2026-11-01**. This is a design contract, not a claim of completed vehicle functionality.

## Canonical visual reference
The user approved the **final 2×2 Aurora mockup** in the 2026-11-01 design discussion (four independent windows: upper-left PIN CEM, upper-right CEM configuration, lower-left DTC, lower-right ECU layout with a **strict side profile** vehicle).
The source image is a 1672 × 941 PNG, generated in the project design discussion. Its conversation attachment is named `a_high_detail_composite_ui_concept_screenshot_cle.png` and is **not copied as a full-resolution raster to this repository yet**. Do not confuse the repository vector specification with the original raster.
The accompanying [wireframe.svg](./wireframe.svg) is a source-controlled **approximate structural trace** for layout review, not a replacement for pixel-perfect screenshot comparison. The screenshot must remain the primary visual target when available. Do not call this implementation pixel-identical without GUI screenshot acceptance.

## Non-negotiable layout principles
1. **One primary function per full-screen workspace**. Four first-class pages: CEM PIN, CEM configuration, DTC faults, ECU blocks. Do not place all four as equally dense cards on one dashboard or overload a screen with unrelated actions.
2. **Simple first, advanced on demand**: screen title, one-line help, one obvious primary action; advanced evidence/addresses inside secondary details, not on the first fold.
3. **Compact Aurora dark theme**: midnight navy (#081326 / #0B1F38), restrained cyan (#19D9EF) and violet (#A988F4), informative accents only for genuine states, 8–12px corners, restrained glow, readable 12–16pt body, accessible contrast, no high-frequency decorative noise.
4. **Stable shell**: Dvinholm logo/name top-left; adapter status, selected vehicle with source, active ECU context; left sidebar with exactly these four priority routes (extra tools may appear separately); compact icon rail on narrow windows. Current session state stays consistent on navigation.
5. **CEM PIN**: P/N + HW/SW identity, stepper, progress with cancellation, secret never shown by default, temporary reveal, protected local vault, operation prerequisites. Do not display invented PIN, progress, vehicle identity or "success" in production UI.
6. **CEM configuration** (VDASH/P3Tool-like organization, not a protocol copy): toolbar (read, backup, compare, prepare), category selector on the left, **parameter list with identifiers and original/current/proposed values** in the center, explanation/allowed values/provenance on the right, staged differences before any write. For P2 the existing source-attributed option manifest is **research-only**: current/allowed/raw fields unknown, editing, read and write must stay disabled until independently verified.
7. **DTC**: one dedicated page with ECU filter, status filter, code, concise human description, active/stored state, selected-fault explanation; explicit read and explicit gated clear. No "clear all" as default, no unauthenticated writing. The current generic UDS implementation supports read only.
8. **ECU blocks**: the selected model chooses a correct **side-on** car drawing. Layout = observed responders list (left), side profile + non-misleading markers (center), selected block details/actions (right). Unknown/unverified ECU names and locations must remain labeled *unverified* rather than green "online". DTC state and basic communication state are independent. A passive response proves only receipt, not ECU identity.
9. **Safety/access**: warnings/actions reflect capabilities; no fabricated P/Ns, physical ECU positions, supported protocols, VIN, parameter IDs/hex mappings or enabled writes. Stable experimental isolation unchanged.
10. **Language**: labels RU/EN, localization must not be mixed randomly. Keep typography readable at 980px/800px/640px window widths. Screen transitions may not start bus activity; active monitor remains under its own start/stop.
11. **Assets/legal**: use Dvinholm-owned logo/banner. Side-profile illustration must be made available per selected vehicle family and license; do not scrape/proprietarily redistribute VIDA/VDASH artwork.
12. **Progress**: implement in bounded PRs, 6–8 new checks per stage, capture actual Windows GUI screenshots, separately gate CI / installed UI / DiCE/J2534 / actual car.

## Reference screen definitions

| Page | First-visible zone | Secondary zone | Disabled until proven |
|---|---|---|---|
| PIN CEM | identity + 3 steps | progress / cancel / vault | unsupported service route |
| CEM configuration | category / parameters / explanation | diffs / backup / provenance | physical read/write, unverified value edits |
| DTC errors | read/filter/code/status/description | selected-fault detail | write/erase until authorized protocol and recovery |
| ECU blocks | model-conditional side car / discovered responses | selected ECU detail, read-only entry | physical position and identity assertions |

## Interaction acceptance
- All seven existing tools and CEM dialogs still work where applicable; four priority routes cannot be dead links.
- DTC clear disabled until a verified service/provider and explicit confirmation.
- No hidden vehicle TX from navigation, category selection, model-image switching, or config parameter selection.
- One display selection cannot overwrite/confirm another ECU or vehicle; clear stale context on disconnect/selection change.
- Full Windows screenshots for all four screens, plus compact widths, compared visually against approved image.
- CEM configuration source manifest remains immutable, cannot infer live hex values from a marketing category.
- ECU model switching must select a different **side-profile silhouette**; unknown model shows generic silhouette with non-physical markers.
- Existing CI x86 build, privacy gates, Stable feature profile and J2534 dispatcher regression continue passing.

## Implementation status
- PR based on `main` after #687: reference specification, SVG approximate structural trace and initial WinUI workspaces.
- Physical PIN support, live configuration reading/writing, erase DTC and physical ECU layout remain separately gated.

## Dedicated DTC workspace: first implementation increment

DTC reading is a separate Aurora destination, placed immediately after CEM
configuration in the sidebar. It owns the existing single-ECU UDS 0x19/0x02
read button and status mask selector; the VIN/DID diagnostics page retains the
explicitly entered TX/RX addressing and other read operations. A navigation
link returns to the address verification page. The shared private diagnostic
result panel is visible on the DTC route, never in the home activity summary.

The screen intentionally shows **no fabricated fault rows, vehicle code
dictionary or cause descriptions**. Real ECU-reported 24-bit codes, supported
status bits and raw statuses use the unchanged existing read formatter and
result lifetime/cancellation gate. Erase is present but disabled; no UDS
0x14/service writing, all-vehicle clear or automatic scan is introduced.
An empty page does not claim the car is healthy. Selecting this page never
sends a request.

Eight new structural/safety tests cover the route, single owned read button,
selector masks, disabled erase, description provenance, private result
presentation and absence of navigation side effects. Windows GUI screenshot
comparison and actual J2534/vehicle acceptance are still separate gates.

## Read-only DTC table (next Aurora increment)

The DTC workspace now binds a table of rows from the same **explicit, single-ECU**
UDS 0x19/0x02 read used by the diagnostic detail result. Column values come
from the ECU response: six-digit hex code, exact raw status byte and only status
flags supported by that ECU's returned availability mask. An unsupported raw
bit remains visible as a discrepancy, not an asserted fault state.

The description column deliberately says **"Нет проверенного описания"** for
all rows. A guessed P-code, Volvo cause, severity, repair action, module identity
or vehicle-wide "no faults" claim is forbidden. For an empty response the
summary says zero records *for the selected ECU and filter*, not a healthy car.
At most 1024 rows are shown, preserving ECU order and reporting the shown
count. Selecting a row reveals only the raw code, status byte, supported mask
and explicit provenance warning.

The preexisting UDS async completion/lifetime guard controls table publication
and text publication together. Changing ECU addresses, DID, DTC filter,
navigation, disconnect or clearing results invalidates the table; a late
response cannot repopulate it. This is intentionally one request, not a
second scan. Erase stays disabled. Eight offline tests cover display fidelity,
masks, unknown states, validation, bounds, XAML, invalidation and erase safety.

Installed Windows GUI and physical J2534/vehicle acceptance remain unverified.

## ECU side-view interaction: observational index rail

The model-switching XC90 P2 / XC70 P3 **side profile stays decorative and
manually selected**. Immediately beneath it an independent scrollable strip of
numbered RX observation tiles is shown after the existing passive discovery.
Each tile includes only a validated response ID and frame count. The numbers
represent **scan result order, never physical ECU location or module
identity**. There are no falsely placed CEM/ECM/SRS hotspots on the body.

Selecting a tile takes the existing `EcusView` selection path; this displays
the existing source-guarded passive response detail, synchronizes the selector,
invalidates the old diagnostic result and explicitly leaves TX blank. It sends
no CAN/UDS message. Incomplete scans show disabled tiles, an empty scan has
no tiles and cannot establish a fault-free vehicle, and the view limits the
visible rail to 20 entries with a truthful count. Rescan/disconnect clear all
buttons and highlights. Model illustration changes do not affect observations.

Eight automated offline tests cover raw RX presentation, incomplete/empty
scans, bounds/order, invalid response rejection, side-view placement disclaimer,
no-send selection and stale context cleanup.

**Still outstanding:** source-verified per-model module locations, automotive
vector/bitmap assets matching the approved visual reference, and real installed
Windows GUI screenshot tests at wide and compact widths. Do not assign
real-world ECU component coordinates based on a screenshot alone.
