# Dvinholm Aurora: approved single-app WinUI 3 x64 design and migration

**User decision: 2026-11-02.** This file supersedes the **WPF / Windows x86 implementation choice** in `DVINHOLM-UNIFIED-WPF-SIX-PAGES-APPROVED.md`. The visual design, layout, and removal of Settings remain approved.

## Final target
- **One user-facing Dvinholm.exe**, WinUI 3, .NET 10, **Windows x64**, **self-contained .NET and Windows App SDK**.
- Do not require separately installing the .NET 10 Desktop Runtime or Windows App SDK runtime. Self-contained **does not imply a single physical PE file**: the app ZIP may include native runtime DLLs and resources.
- Hardware J2534 vendor DLLs and their OS drivers remain external; the app must never falsely claim they're bundled.
- **Exactly six top-level sidebar destinations:** Подключение / Connection, Обновление / Updates, CEM PIN, Конфигурация CEM / CEM Configuration, Ошибки (DTC) / Faults, Блоки (ECU) / Modules. **No Settings page.**
- Maintain original approved dark navy / cyan / violet Aurora glass cards, narrow sidebar with northern lights, icon #9 gear/gull/arrow, live top bar, RU/EN switch and context-relevant controls.
- Any vehicle/VIN/CEM and reported driver state is real evidence only. Mocked illustrated vehicle identifiers, PIN percentages, ECU topology, software versions and DTC values are not real app data.

## J2534 bitness is a hard gate
- A 64-bit process **cannot load a 32-bit J2534 PassThru DLL**. Driver enumeration must read the **64-bit registry view for x64**, and 32-bit view only for x86. Do not offer a 32-bit-only driver as a connectable x64 adapter.
- Specifically verify **Volvo DiCE** and **Mongoose JLR Pro** real supplied DLL architectures before accepting hardware support. A driver that only ships x86 is **incompatible with direct x64 in-process loading**.
- Do **not** silently introduce a second x86 helper or cross-bitness IPC bridge. If needed, document it as a separately reviewed architecture change, rather than pretending that x64 directly supports legacy x86 adapters.
- Driver absence or mismatched bitness must keep Connect disabled with a specific explanation. Virtual adapter offline testing does not prove physical adapter compatibility.

## Unified updater
- Channel selector Stable/Development and persisted choice live inside the **Updates** page.
- Reuse verified SHA-256, trusted provenance, transaction staging, clean restart, recovery/rollback, and fail-closed states from existing updater code.
- Running process self-replacement requires **short-lived helper after the app exits**, not a separate visible Launcher app.
- The x64 updater must only select **verified win-x64** assets and must never install legacy win-x86 archives.
- No auto-install until explicit user confirmation; upgrades and rollback must be tested with interruption/restart and an offline fixture.

## Migration boundaries and safety
1. **Phase 1: x64 build and compatibility** (this PR). Create a stand-alone x64 WinUI 3 self-contained artifact on Windows CI, validate architecture and bundled runtime contents; retain x86 production CI only as transitional compatibility. The x64 artifact is for **PR review only**, not a release.
2. **Phase 2: six-page native WinUI Aurora shell**. Reuse actual CEM, DTC, ECU, J2534 code from existing WinUI 3 project, and reorganize UI into six pages; remove Home, Settings, diagnostic demo routes from top-level nav without deleting underlying read-only functionality.
3. **Phase 3: integrated update controls**. Wire channel, check, verified download, install and rollback into Updates page; keep installer actions disabled until tested and explicit user approval.
4. **Phase 4: J2534 x64 adapter discovery and connection**. Verify 64-bit native PassThru DLL and real hardware; explicit errors when incompatible.
5. **Phase 5: release acceptance**. Verify DPI 100/125/150, RU/EN, navigation, actual Windows x64 boot, unchanged data migration, safe offline mode, clean restart, authenticated upgrade, no prototype or demo Stable leakage.

For each stage use **6–8 substantive checks** instead of long chains of micro-fixes. Keep Stable and independent Virtual Car untouched until actual production acceptance and release. **Do not merge the superseded WPF PR #701 or WinForms Launcher PR #699.**

## Release policy
- No x64 Stable release until CI and physical Windows acceptance. The old x86 updater feed must remain functional for installations already in the field while the new unified x64 feed is developed.
- Backward compatibility must be explicit; an x64 package cannot silently update an x86 install with a different driver architecture or an incompatible payload.
