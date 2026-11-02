# Dvinholm Unified WPF: approved six-screen Aurora design

Status: **DESIGN APPROVED by user (2026-11-02)**. This document is a UI/UX contract, **not** evidence that the screens, CEM operations, or updater have been implemented.

## Product decision

- Replace the separate user-facing `Launcher.exe` and the WinUI 3 main application with a **single user-facing `Dvinholm.exe` built with WPF / C# for Windows x86**.
- Move updater, J2534 driver discovery/connection, CEM research, configuration, DTC and ECU views into this unified application.
- Do not silently install .NET 10 Runtime, Windows App SDK or other prerequisites. The objective is an application requiring **no separate app-runtime installer** on supported Windows 11, while recognizing that hardware-specific **J2534 vendor drivers are external and still required**.
- The existing updater's verified-download, SHA-256, transaction journaling, rollback, channel persistence, recovery and failure gating are safety requirements, not expendable UI features.
- Replacing an executing EXE cannot be done in-place; an isolated short-lived helper may apply a verified update **after Dvinholm.exe exits**. This helper is **not** a second user-facing launcher. Preserve atomic staging, restart and rollback safety.
- **Runtime/portability feasibility gate:** the present WinUI 3/.NET 10 backend cannot simply be referenced from .NET Framework 4.8 WPF. Audit APIs and NuGet dependencies first, then choose and document between (a) porting shared code to compatible targets for WPF on preinstalled .NET Framework, or (b) WPF on **self-contained** modern .NET x86 (larger footprint, no external .NET runtime installation). Do not promise zero OS or vendor dependencies.
- Main application must remain functional offline for all local diagnostic, read-only, UI and recovery functions that do not intrinsically require network.

## Approved visual contract

The user approved **all six page mockups** based on their supplied compact navy/cyan/purple reference (2026-11-02). These image-generation mockups are visual guides, not screenshots of working software.

**Persistent shell, all pages:**
- Dark navy application chrome, one narrow **left navigation sidebar**, vivid cyan → blue → violet highlights and thin glass-like card borders.
- Top brand area `Dvinholm`, approved **gear/seagull/arrow icon #9** from the existing branding decision; a triangular auto-generated symbol in the illustration is **not** a replacement logo.
- Persistent **top status bar** with actual `J2534` connection state, exact detected adapter name, verified vehicle identity and CEM P/N if truly read. Show explicit `Not connected` / `Not checked` instead of placeholders when absent.
- Sidebar contains exactly six main items, from top: **Подключение**, **Обновление**, **CEM PIN**, **Конфигурация CEM**, **Ошибки (DTC)**, **Блоки (ECU)**. There is **no Настройки / Settings navigation item or separate Settings page** in the approved design. RU and EN use the same layout.
- Keep relevant controls contextual: J2534 adapter and CAN parameters in **Подключение**, update channel and behavior in **Обновление**, and RU/EN language selection in the compact top bar. Do not add a seventh screen or a settings drawer without a new design decision.
- Cyan/violet active navigation marker; all inactive labels remain readable. No decorative dead buttons or clickable-looking placeholders.
- Northern-lights atmospheric graphic in the sidebar only, keeping tables and controls legible. Dark matte content background with blue/purple focus and intentional green/amber/red system states.
- Consistent typography and density, bounded window/minimum size, DPI-aware WPF scaling, keyboard access, accessible focus indication, no horizontal clipping at 100%, 125% and 150%.
- Use visual mockups from the 2026-11-02 conversation for spacing, hierarchies, pane sizes and glow. Use editable WPF/XAML controls and text rather than baking UI text into raster assets.

## Six approved pages

### 1. Подключение
- Two-column layout: left J2534 adapter selection + connection parameters, right driver state and connection log.
- Separate adapter discovery from successful connection. J2534 enumeration must not mean a physical car is connected.
- Detect installed vendor PassThru drivers, including Volvo DiCE and Mongoose JLR Pro when actually present.
- Show protocol/speed and optional auto-detect; never silently assume a model-specific CAN bitrate. Explicit Connect/Disconnect commands and errors.
- Header shows vehicle identity only after valid ECU evidence. Example Volvo XC70 (P3) 2012 / CEM 31343352 in artwork is **illustrative only**.

### 2. Обновление
- Current build vs available verified build and **Stable / Development** channel selection, with persisted channel.
- Changelog, SHA-256 verification, verified signer/provenance requirements when available, prerequisites/preflight, download/install progress, restart and rollback/recovery status.
- The illustrated release numbers `v2.5.0`, `v2.6.0`, dates, sizes and progress values are **mock data**, never hardcode or use as shipping version.
- Confirm update before replacing files; preserve functioning installation if validation fails.
- Update controls remain inside Dvinholm; self-replacement helper runs only after the main app closes.

### 3. CEM PIN
- Three-stage UX: **Идентификация → Поиск PIN → Результат**, module P/N/HW/SW, progress and cancellation.
- **Must fail closed**: no simulated PIN extraction success or artificial percent; show real state only.
- This page is a feature target, not proof that current CEM PIN services/algorithms are known. Research/probe-only mode until verified authorized protocol, ECU revision, recovery and bench safety gates are met.
- Missing real capability must be shown clearly as `Недоступно / требуется проверка`; never offer an operable bypass.

### 4. Конфигурация CEM
- Three-column layout: categories at left (**Основные, Комфорт, Освещение, Аудио, Безопасность**, extensible), parameter grid center (**Параметр / Исходное / Текущее / Новое**), selected parameter documentation at right.
- Footer action bar: **Прочитать**, **Резервная копия**, **Сравнить**, **Подготовить изменения**, **Записать в CEM**.
- Read-only and offline draft flows may be enabled where supported. **Write** stays disabled without verified ECU-specific encoding, a readback, valid backup, authorized request, compatibility check and safe recovery plan.
- The reference mockup parameter numbers/values are illustrations, not validated P3 CEM byte maps.

### 5. Ошибки (DTC)
- Filter by ECU and status, refresh/read, selectable rows with code/description/status, large details pane for diagnosis.
- Clear DTC is a high-consequence, confirmation-gated action and only enabled if actual ECU response, protocol, permissions and safety conditions support it.
- Separate `активная`, `сохранённая`, `нет ответа`; no invented DTCs in a real session.

### 6. Блоки (ECU)
- Left scrollable actual discovered ECU inventory/status; center vehicle topology with module nodes; right selected ECU identity and real actions.
- Accurate count of responsive modules, errors, nonresponders and total, generated from real session state only.
- Vehicle graphic and label positions adapt to selected profile (not hardcoded to illustrated XC70).
- ECU diagnostic/data controls navigate to implemented functions; do not promise unavailable active service operations.

## Technical separation and implementation principles

- WPF **view + view-model** must be independent of transport and updater state machines. Use clear cancellation, error/timeout reporting, worker-in-progress gating, nonblocking UI and audit logs.
- Migrate or adapt the proven J2534 RX dispatcher / TX serialization / ISO-TP / CAN monitor and tested updater transaction logic rather than reimplementing transport in page event handlers.
- Keep `Development` and `Stable` feature gates. No demo, fake PIN, fake ready, unsafe programming or test-profile actions in Stable.
- Portable data migration: preserve existing profile JSON and user preferences, with backups and migration validation.
- Keep separate **Virtual Car** project independent; may use its test fixtures but do not build or publish it in this migration.
- Do not break old releases or delete the proven launcher path until a new unified path passes full offline regression plus manual Windows install/update/recovery.

## Stage gates (6–8 checks each, no long micro-fix chain)

1. **Foundation (8):** WPF x86 build, launch on clean supported Windows, no separate runtime download, exactly six items/no Settings, RU/EN, DPI 100/125/150, safe initial state, clean shutdown.
2. **Connection (8):** enumeration, missing driver, wrong-bitness DLL, connection failure, correct disconnect, RX timeout, cancellation, virtual adapter read-only path.
3. **Updates (8):** Stable/Development, remembered channel, authentic manifest, digest/signature/provenance, staged download, failure recovery, verified restart, rollback.
4. **Diagnostics (8):** CAN/ISO-TP baseline, ECU inventory, DTC filtering, no-response, benign read-only service, no unverified write, saved profiles, status/log parity.
5. **CEM (8):** read-only identity, offline research, supported/unsupported revision gating, parameter draft, backup, compare, unauthorized PIN rejection, write disabled without real evidence.
6. **Release acceptance (8):** Windows x86 install/launch, DPI, RU/EN, complete screen navigation, self-update across channels, offline/rollback, packaging audit, zero demo leakage.

**Real physical Volvo DiCE/J2534 and ECU hardware tests remain separate unperformed acceptance gates.**

## Superseded design work and publication policy

- This approved **single-application WPF** direction supersedes the earlier **WinForms Aurora Launcher / PR #699** as a release architecture; it can be consulted as an artwork reference, not merged as the final approach.
- Existing Stable remains unchanged until the WPF app is validated and a real release is intentionally published. This design approval alone authorizes no production release.
