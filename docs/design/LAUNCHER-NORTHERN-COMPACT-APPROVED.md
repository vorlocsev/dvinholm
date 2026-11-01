# Dvinholm Launcher: Northern Compact Aurora (approved)

**Approval:** user selected the **fourth / last variant** from the four compact
Launcher Aurora mockups on 2026-11-01. The design concept is a compact centered
launch window with a Volvo vehicle, a fused Northern Dvina / old Severodvinsk
gull-and-gear emblem, White Sea waves and northern aurora light.

This document is a **design acceptance contract**, not an assertion that the
first implementation is pixel-identical to the generated mockup. The original
approved concept is an in-conversation image and has **not** been added as an
original-resolution binary file to the repository. The existing bundled
`assets/launcher-service-cockpit-banner.jpg` is reused as the interim
automotive panorama; the emblem is currently painted with GDI+ from original
vector primitives. Do not imply exact artwork assets have already been ported.

## Compact home

- **Window:** fixed-size WinForms PowerShell 5.1 x86-friendly application,
  client footprint at most **944×552 px** on a normal desktop; work-area-safe
  proportional scaling and explicit scrolling on constrained monitors.
- **Hero:** dark navy Aurora, northern lights and car panorama, a merged
  *ring-arrow / gull / gear / White Sea waves* Dvinholm identity, and an
  original `DVINHOLM LAUNCHER` wordmark.
- **Information hierarchy:** prominent, single **Start Dvinholm** action;
  actual `.NET x86`, `WinApp Runtime` and local J2534 driver *inventory*
  readings; separate compact **Updates, Components, J2534 Drivers, Settings**
  controls; current version/channel/operation status along the bottom.
- **Navigation:** main **Updates** button reveals the pre-existing full
  Service Cockpit including real `Check`, `Install`, `Versions`, Windows
  dependencies, driver inventory, progress and safe handoff. The Back/Home
  button returns to the compact home. No dead navigation.
- **Language:** RU and EN retained; selection persists through the existing
  small safe language preference file in `%LOCALAPPDATA%`.
- **Trust:** **never** show a green readiness check without worker-provided
  installed version, runtime prerequisites and validated updater recovery.
  Driver registration is **not** proof of physical adapter/ECU connectivity.
  Do not infer identity from the art.
- **Safety:** the native `Launcher.exe` remains independent of .NET 10 and
  WinApp Runtime. Existing worker scripts, SHA-256 verified artifacts,
  Stable/Development choice, native self-update and rollback, confirmation
  prompts, driver/installer safety, locale, lockout and closing-after-launch
  behavior must remain unchanged.

## First implementation

`eng/Launcher-ServiceCockpit.ps1` adds the new **Northern Home** using
inbox WinForms/System.Drawing. The previous Service Cockpit UI is preserved
behind a real Updates view rather than deleted. The large Start button forwards
to the actual existing launch button and observes its `Enabled` state.
Components, Drivers and Settings are connected to the original dialogs.
Existing worker snapshot values are shown in the compact footer; visual labels
do not invent a release version or installed dependency state. The rendering
of a combined emblem is a GDI+ sketch to be polished in subsequent visual
iterations, not a redistributed Volvo trademark graphic.

`eng/Updater-Banner.ps1 -Mode UiSmokeTest` now includes eight additional
Windows PowerShell 5.1 offline assertions: default foreground, compact bounds,
brand/primary action order, live secondary actions, truthful dependency
readiness, Start fail-closed, Updates/back navigation and RU/EN labels.
None of these start an installer, use network/vehicle, or publish releases.

## Manual Windows GUI acceptance (not yet performed)

1. Compare actual Start home with the user-approved **fourth** mockup at
   normal DPI, using screenshots rather than only XML or element bounds.
2. Inspect 100%, 125%, 150% Windows DPI and 640–944 effective client widths;
   no clipped controls, giant blank gaps or off-screen dialogs.
3. Confirm live progress and error messaging; channel selection persists
   through restart and switches between Stable and Development.
4. Open Updates and return to Home, run safe offline component check,
   open J2534 driver inventory, Settings and RU/EN.
5. Verify installer confirmations and recovery locks; no click-through
   beneath the overlay, no retry when a helper is already running.
6. Verify first launch and successful auto-close, failed launch stays open;
   no dependency is claimed installed until verified.
7. Check normal and missing-bitmap fallback render (LTSC/WIC-unavailable).
8. Compare actual side-profile Volvo photography and final fused emblem to
   the **approved raster**. Replace the interim art only with appropriately
   licensed original assets, retaining model and branding accuracy.

**Explicitly not claimed:** pixel-perfect acceptance, physical DiCE/J2534
testing, new Stable release, or changes to the separate Virtual Car project.
