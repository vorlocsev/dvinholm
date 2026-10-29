## Launcher Aurora: баннер, настоящая навигация, RU/EN и закрытие

После переработки Launcher сохраняет компактную клиентскую область до
**944 × 552 px** при обычном масштабе (меньшие экраны используют подгонку
к рабочей области и прокрутку). Увеличен видимый верхний баннер.
`assets/dvinholm-banner.webp` входит в комплект как
`Binaries/dvinholm-banner.webp`; когда на Windows доступен декодер WebP
через WIC, отображается исходный репозиторный баннер. На Windows LTSC без
такого кодека отображается нарисованная средствами WinForms Aurora.

Каждый пункт бокового меню имеет конкретное действие:
**Запустить Dvinholm**, **Проверить обновления**, **Проверить компоненты**,
**Настройки**, **О программе**, **Драйверы адаптеров**.
В настройках выбираются Stable/Development и Русский/English; язык
сохраняется отдельно в ограниченном обычном файле
`%LOCALAPPDATA%\Dvinholm\UpdaterBanner\language-preference.txt`.
Аварийный откат и восстановление прерванной установки находятся в
настройках и включаются только при подходящем проверенном состоянии.
На главном экране остаются прогресс, версии, компоненты и запуск.

Для запуска выбранного приложения Launcher использует свой доверенный
`Manage-Lite-Dependencies.ps1`, а не произвольный скрипт из
загруженного каталога. Вспомогательное окно зависимостей возвращает код
**41**, если закрыто без создания процесса Dvinholm. Только когда
проверенный Launch worker завершился без ошибки и основной процесс был
успешно создан, окно Launcher автоматически закрывается. При любой
ошибке или отмене Launcher остаётся открытым с диагностикой.

Проверки PowerShell 5.1 CI подтверждают алгоритм, но реальное отображение
оригинального WebP и закрытие после запуска на Windows 11 LTSC нужно
отдельно подтвердить вручную. Это не проверка физического J2534.

## Компактный интерфейс Launcher (Aurora)

Основное окно Aurora имеет укороченный баннер и автоматически
уменьшается до **944×571 px** по клиентской области вместо прежних
1180×790 px. По фотографии реальной Windows обнаружено обрезание
подписи внизу боковой панели и слишком тесное размещение нижних кнопок.
Баннер сокращён с 232 до 156 px до масштабирования, рабочие панели
подняты на 76 px, подпись в меню размещена выше, а снизу оставлен запас. При запуске размер
подстраивается под рабочую область текущего экрана Windows, исключая
панель задач и рамку окна. Если доступная область ещё меньше, включается
прокрутка без удаления важных действий.

Панорама/баннер, логотип, шрифты, карточки, боковое меню и кнопки
масштабируются вместе. Это касается установки и восстановления,
`Check runtime versions` и выбора Stable/Development. Компактная
компоновка не влияет на механизм обновления, проверку SHA-256,
зависимости, драйверы J2534 и их установку. Проверка Windows PowerShell
5.1 создаёт реальную WinForms-форму без открытия окна, проверяет
доступные размеры, нижние кнопки и блокировку действий.

## Application startup handoff

Launcher starts the selected installation's `Manage-Lite-Dependencies.ps1 -Mode Launch`
through the Windows system PowerShell executable. It no longer opens the
`Start-Dvinholm-Lite.cmd` file through its Windows file association. The same
strict prerequisite check and dependency UI remain in use. Paths are passed
as literal `-File` arguments; no `cmd.exe` expansion is involved.

The worker waits for the helper process (not the WinUI application's lifetime)
and preserves a nonzero helper exit code. A completed helper is not proof that
the main window rendered successfully. A launch failure now includes the
underlying error instead of only a generic "Unable to open" message.

Eight Windows self-tests cover six path forms (plain, spaces, ampersand,
percent/environment-variable text, apostrophe/brackets and Unicode), a failed
helper and a missing helper. They launch only a synthetic PowerShell fixture.
User acceptance on 2026-10-29: the runtime report was visible and manual
`Start-Dvinholm-Lite.cmd` startup opened the application after a Windows prompt.
The repaired Launcher route still needs acceptance on that user's PC.

## Основной дистрибутив и драйверы J2534

**Основной дистрибутив Windows x86:** `Dvinholm-Windows-x86.zip`.
Это прежде называвшаяся Lite-конфигурация (.NET 10 Desktop x86, Windows
App Runtime и VC++ устанавливаются отдельно через Launcher), теперь
без слова Lite в пользовательском названии. В архиве два нативных
исполняемых файла `Launcher.exe`, `Dvinholm.exe` и папка `Binaries/`.
Файл `Dvinholm-Windows-x86-Lite.zip` пока публикуется *исключительно
как совместимый канал обновления старых версий Launcher*, не как другая
сборка приложения. Отдельного self-contained Portable нет.

Раздел **Драйверы адаптеров** в Launcher работает до установки .NET 10.
Он проверяет регистрацию J2534 в 32-битном реестре, наличие DLL и
заголовок **PE32 x86 DLL** (не EXE и не PE32+), а отдельно обнаруживает
USB **Volvo DiCE** по Windows PnP и
регистрацию **MongoosePro JLR** в J2534. Для USB DiCE дополнительно
различаются состояния Windows PnP **OK / Problem / Unknown**; при отсутствии
`Get-PnpDevice` используется запасной опрос `Win32_PnPEntity` через CIM.
Ни один из этих статусов не подтверждает связь с автомобилем.
Не вызывает PassThruOpen
и не утверждает, что проверил реальную работу оборудования.

- **MongoosePro JLR**: официальный каталог загрузок
  https://opusivs.com/support/product-downloads/ . Для Dvinholm x86
  требуется зарегистрированная 32-битная J2534 DLL. Запись в реестре
  и DLL не гарантируют связь с подключённым устройством.
- **Volvo DiCE**: официальные инструкции
  https://www.volvospecialtools.com/index.cfm?event=info.dice .
  Volvo VIDA поддерживает DiCE и J2534 как *различные* интерфейсы;
  появление USB-драйвера DiCE не делает его J2534 совместимым.
  Подключение DiCE напрямую к Dvinholm потребует отдельного backend.

Кнопки «Установить ...» требуют локального оригинального пакета
производителя (.exe/.msi) с действительной и доверенной цифровой подписью,
для конкретных моделей дополнительно проверяется производитель
подписи (Volvo либо Drew Technologies/OPUS IVS). Перед запуском видны
имя файла, издатель и отдельное подтверждение пользователя. Установка
осуществляется стандартным видимым установщиком и запросом UAC.
Launcher ничего не скачивает и не устанавливает без запроса,
не загружает неизвестные DLL, не выполняет поиск VIN/PIN и не
перепрошивает адаптер или ECU. После завершения установщика Launcher автоматически повторяет локальное
обнаружение J2534 и DiCE, если окно драйверов открыто. Ошибка установщика
не считается успешной установкой; работоспособность физического
адаптера по-прежнему требует отдельной проверки на Windows.
При коде завершения установщика **3010** или **1641** Launcher отдельно
показывает требование перезагрузки, не объявляет драйвер готовым и предлагает
повторить проверку после перезапуска Windows. Сканирование заголовка PE
не проверяет цифровую подпись DLL, экспорт функций PassThru или связь с
физическим адаптером. При необходимости пользователь перезагружает компьютер.

## Завершение установки пререквизитов Microsoft

Коды завершения установщиков Microsoft `0`, `3010`, `1641`
обрабатываются отдельно. `0` допускает автоматическую повторную
локальную проверку. `3010` и `1641` означают успешную установку с
необходимостью перезагрузки: Launcher показывает соответствующее сообщение
и не объявляет пререквизиты готовыми без перезапуска Windows.
Другие коды считаются ошибкой или отменой, без автоматического
повторного опроса.

Старый snapshot обновлятора удаляется после завершения дочернего
установщика, чтобы таймер интерфейса не затёр его итоговое сообщение.
Самостоятельный интерфейс `Manage-Lite-Dependencies.ps1` соблюдает то же
разделение результатов. Проверки этих кодов выполняются на синтетических
данных без запуска MSI/EXE.

## Проверка совместимости установленной сборки

Кнопка **Check runtime versions** показывает два независимых результата:
наличие/версии компонентов Microsoft и **Strict compatibility** для
выбранной сборки Dvinholm. Для установленного канала
Stable/Development Launcher запускает собственный
`Check-Lite-Prerequisites.ps1` из комплекта Launcher в режиме только
чтения, передавая каталог проверенной сборки как данные.
Он проверяет `runtimeconfig.json`, .NET roll-forward, необходимые
x86-пакеты Windows App Runtime и VC++. Исполняемый файл Dvinholm,
скрипты из проверяемого канала и адаптеры J2534 не запускаются.

- **Compatible:** строгая проверка прошла; это ещё не тест открытия окна.
- **Incompatible:** точная проверка нашла отсутствующий или несовместимый
  компонент и показывает ограниченное диагностическое сообщение.
- **Not installed / Incomplete installation:** нет полного набора
  исполняемого файла и runtimeconfig.
- **Not verified:** отсутствует доверенный скрипт проверки, обнаружена
  небезопасная ссылка на файл или канал находится в состоянии восстановления.

Если канал пока не установлен, проверяется только комплектная сборка,
если она имеется, с пометкой **Bundled application (not channel-specific)**.
Версии .NET и сведения о доступных обновлениях доступны и без
установленного Dvinholm. После прохождения проверки совместимости
остаётся необходима проверка запуска GUI на реальной Windows 11.

## Проверка пререквизитов и новых версий

В Aurora Launcher сохранена кнопка **Recheck deps**. Она работает только
локально и не требует интернета: проверяет наличие .NET Desktop Runtime 10
x86, Windows App Runtime и VC++ Redistributable x86. Для установки и запуска
по-прежнему выполняется строгая проверка опубликованной конфигурации
`Check-Lite-Prerequisites.ps1`.

Дополнительная кнопка **Check runtime versions** запускает *явную* фоновую
проверку с отдельным результатом для всех трёх компонентов: установленная
версия, факт обнаружения обязательных пакетов, последняя версия из
проверяемого источника и возможность сравнения. Проверка ничего не
скачивает на диск и не устанавливает.

- **.NET Desktop Runtime 10 x86:** установленную версию считываем
  через x86 `dotnet.exe --list-runtimes`, актуальную стабильную 10.x
  через официальный Microsoft `releases.json`. Отдельно показываем
  **Microsoft.NETCore.App x86**; отчёт версий сравнивает **Desktop
  Runtime x86 и Core Runtime x86 независимо** с официальной последней
  стабильной версией .NET 10. Отсутствующий Core получает `Missing`,
  устаревший Core получает `Update available`, отсутствие сети
  отображается как `Latest version not verified`. Для локального
  статуса `READY` должны присутствовать обе среды общей линии 10.x
  (Desktop + Core). Точная совместимость с политикой `rollForward` и
  минимальной версией по-прежнему определяется строгим
  `Check-Lite-Prerequisites.ps1` при запуске.
- **Windows App Runtime x86:** установленная версия показывается по
  x86 MSIX Framework `Microsoft.WindowsAppRuntime.2`; актуальная
  стабильная версия SDK берётся из официального GitHub
  `microsoft/WindowsAppSDK/releases/latest`. Версия MSIX пакета и версия
  SDK не идентичны: сравнивается *линия major.minor*, а не patch/build.
  Более новая версия SDK только информационная; установленный и
  протестированный runtime 2.5.1 не заменяем автоматически.
- **VC++ Redistributable x86:** локальная версия читается из записи
  `VC\Runtimes\x86` (с проверкой Installed и major 14).
  Постоянная ссылка Microsoft `https://aka.ms/vc14/vc_redist.x86.exe`
  предоставляет обновляемый установщик, но не подтверждённую
  машиночитаемую версию. Поэтому актуальность обозначается
  **Latest version not verified**, а не выдуманным «Up to date».

Ошибки сети не превращаются в статус «драйвер отсутствует» и не отключают
работающий Dvinholm. Все установщики по-прежнему требуют отдельного
подтверждения пользователя и действительной подписи Microsoft. Статус
найденного компонента не означает успешного запуска WinUI на конкретном ПК.

# Dvinholm Launcher (native Windows x86)

The new **Launcher.exe** is a small native Windows x86 bootstrap, compiled with
the static MSVC runtime. It needs only Windows PowerShell 5.1 and the inbox
.NET Framework WinForms to display the updater; **it does not require .NET 10,
Windows App SDK, VC++ Redistributable, Qt or Electron** to start.

For a first-time installation, use the **main** Development asset
`Dvinholm-Windows-x86.zip` and extract the full directory:

```text
Dvinholm/
  Launcher.exe           # update/check/repair/runtime management
  Dvinholm.exe           # opens the selected app (or bundled first-run Lite)
  Binaries/
    Dvinholm.App.exe     # WinUI 3 diagnostic application
    Dvinholm.App.dll
    ...                  # other Lite runtime files and assets
    Updater-Banner.ps1
    Manage-Lite-Dependencies.ps1
    Dvinholm-icon.png
```

`Launcher.exe` owns channel selection, checks, downloads, install/rollback,
recovery and Microsoft dependency management. The WinUI diagnostic application
**has no Check update button or in-process installer handlers**. The launcher uses the approved **Aurora** dashboard: a panoramic northern-lights
header with a vehicle silhouette, Dvinholm logo, blue-accented cards, a left
navigation strip, Stable/Development channel radio cards, dependency controls,
version summaries, and download progress based on real ZIP bytes. Non-download
phases use a busy indicator. The graphics are drawn with built-in Windows
GDI+/WinForms rather than Qt, Electron, WebView or a heavyweight image runtime.
Channel radio cards become disabled during worker operations. This visual
layer does not change integrity verification, Microsoft installer confirmation,
separate channel folders, or crash recovery.

A much smaller `Dvinholm-Updater-Banner-Windows-x86.zip` is also published
for users who need **only the standalone launcher**. It has `Launcher.exe`
and its support files in `Binaries/`, but no diagnostic app; click
**Download / Install** to create one. Existing flat
`Dvinholm-Windows-x86-Lite.zip` remains a compatibility feed for older
clients and for the launcher's integrity-checked update mechanism.

`Dvinholm.exe` first checks for a verified installed version of the channel
last selected in Launcher. On a pristine extraction, it can start the bundled
`Binaries/` Lite app through its prerequisite-aware startup command.
After updating, the active selected channel's managed copy takes priority.
The package must be extracted in full; neither executable should be moved
away from the `Binaries/` folder.

**Where updates are stored:** the existing proven transactional engine keeps
separate per-channel `current` and `previous` folders under
`%LOCALAPPDATA%\\Dvinholm\\UpdaterBanner`, while the extracted
`Binaries/` folder is the initial fallback. This separation allows safe
rollback without overwriting the running native launcher or a user-supplied
manual installation. Existing Lite installations are not silently modified.

While a Microsoft installer or any Banner worker is running, all three
installer buttons, channel selector and update controls are visually disabled.
Successful Microsoft installer completion triggers an automatic **local-only**
dependency probe. A failed or cancelled installer shows its exit code and
requires a manual **Recheck deps** instead of clearing the failure message.
Windows CI exercises seven synthetic WinForms vendor-worker lifecycle checks;
a real Microsoft installation and UAC prompt remain manual acceptance steps.

The **Installed** label is validated against the selected channel's actual
Lite directory, required ordinary files, metadata and x86 PE header. It
reports **Unverified installation** rather than trusting a stale manifest if
files are missing, corrupt or from another channel. Stable displays only the
version number (for example, `0.2.4`) for both Installed and Available;
Development continues to display `ci-N`. Canonical GitHub tags like
`v0.2.4` are still used for download verification and transaction journals.

The banner provides a **Stable / Development** selector, installed and
available versions, three dependency statuses, a check button, a
separately confirmed **Download / Install** button and three independent
Microsoft installer/update buttons. Downloads are only started after
the user clicks and confirms; Windows UAC and vendor setup UI remain visible.

**Already installed:** when the selected channel's verified Lite version
matches the latest public release, **Check updates** reports *Up to date*,
and **Download / Install** performs no download or file replacement. If the
latest published version is numerically **older** than the managed
installation, installation is blocked; **Restore previous** is the separate,
explicitly confirmed offline rollback path. Stable compares semantic
`vX.Y.Z` versions, while Development compares numeric `ci-N` build
numbers. Only the currently selected channel's managed installation is
considered. CI covers eight offline current/newer/older/first-install and
wrong-channel-tag conditions; no real Windows GUI acceptance is implied.

The **Download / Install** worker now performs an additional offline
preflight **before any GitHub release request or ZIP download**. It requires
positive local detection of .NET 10 Desktop x86, Windows App Runtime and VC++
x86. If a dependency is missing or cannot be determined, installation stops
without creating a download/staging directory. Use the corresponding
Microsoft installer button, then **Recheck deps** and retry. The existing
checks of the downloaded Lite files and runtimeconfig remain mandatory before
the installed version is changed. Seven offline negative/positive preflight
checks run in CI; actual Microsoft installer behavior still requires Windows
manual acceptance.

The banner remembers your last selected channel in the per-user
`%LOCALAPPDATA%\Dvinholm\UpdaterBanner\channel-preference.txt` file.
Only the literal values `Stable` and `Development` are accepted; an
invalid or inaccessible preference falls back to Development. An explicit
`-Channel` parameter overrides the saved choice when starting the script.
**Recheck deps** runs only the local dependency inventory, even when the
computer is offline; it does not query GitHub. Use **Check updates** to
refresh the available build. The local recheck clears the previously shown
available-version label until the next explicit or channel-switch check.

- **Stable** resolves the latest public GitHub vX.Y.Z release.
- **Development** resolves the newest public ci-N prerelease with a
  SHA-pinned main branch target and a Lite archive.
- Releases must contain a uniquely named Lite x86 ZIP and SHA256SUMS.
  Both asset SHA-256 digests are independently checked against the GitHub
  REST API; the downloaded sums file must also match the ZIP contents.
- Untrusted archive paths, duplicate names, links, oversized entries,
  invalid x86 executables and missing files fail closed. A downloaded Lite
  runtimeconfig is checked on the **actual local Windows host before
  replacing any binaries**.
- Microsoft-only runtime setup is delegated to the existing dependency
  manager. It checks a Microsoft signature on installers, checks .NET
  SHA-512 release metadata, and leaves installers/UAC visible.
  Windows CI separately checks eight synthetic .NET 10 Desktop x86 installer
  metadata scenarios through `Manage-Lite-Dependencies.ps1 -Mode SelfTest`:
  valid selection, missing/preview versions, duplicate matching releases,
  wrong architecture, duplicate files, untrusted URLs and invalid SHA-512.
  This test runs without internet access or executing any installer; it does
  not replace a real dependency-installation test on Windows.
- No scheduled or unattended install; no elevated or silent commands.

Managed installation folders are distinct:

    %LOCALAPPDATA%\Dvinholm\UpdaterBanner\Stable\current
    %LOCALAPPDATA%\Dvinholm\UpdaterBanner\Development\current

A successful update leaves one prior version in the channel's
`previous` directory. Existing manual Dvinholm installations are **not**
modified automatically. This prevents accidental downgrades when switching
channels. Application ECU profiles and settings remain in the existing
separate user-data location. The banner's **Start Dvinholm** button now delegates to a separate
per-channel PowerShell worker, which rechecks recovery markers and validates
the selected `current` folder's Lite files, channel metadata and x86 PE
header **immediately before launching** the dependency-aware startup command.
The GUI does not launch an unchecked `current` batch file directly. A
missing/corrupt installation, cross-channel metadata, or unresolved
install/rollback blocks startup without changing installed files. These are
structural safety checks, not a substitute for cryptographic verification of
an installation after it has been modified on disk.

**Restore previous** performs an offline, separately confirmed rollback of
the **currently selected channel only**. Close Dvinholm before invoking it.
The launcher validates both managed versions' manifest channel, expected
Stable/Development tag syntax, required Lite files and x86 PE header before
moving anything. It refuses cross-channel, missing, invalid or identical
backups. A successful rollback swaps `current` and `previous`, allowing
the replaced build to be restored again. A per-channel Windows mutex prevents
two banner workers from modifying the same installation simultaneously.
On an interrupted swap, the launcher attempts to restore the original
directory names and reports if manual recovery is necessary.

Before enabling updates, launches or rollback in an ordinary clean
channel, the banner also verifies the on-disk **current** and **previous**
Lite folders. A broken x86 executable, missing Lite file, wrong-channel
metadata or orphaned `previous` without `current` yields
**Recovery: Inspect**, not **Ready**. This is a read-only guard: it does not
erase or automatically repair the folders. A clean first installation
without any `previous` remains supported, as does an empty channel.
Seven synthetic offline folder-state checks cover these cases; actual
recovery and installed Windows UI behavior remain separate.

**Repair rollback** is a separate, explicitly confirmed, **offline**
recovery action for a rollback interrupted by process termination or power loss.
It never silently repairs a directory on startup. It accepts exactly one
`rollback-<32 lowercase hexadecimal characters>` staging directory and only
two recognizable incomplete layouts: either `current` is absent while
`previous` and the staging directory are valid, or `previous` is absent
while `current` and the staging directory are valid. It verifies both
surviving Lite installations and their channel/versions before moving files.
In either case it restores the original `current` and `previous` pairing.
If a repair is interrupted midway, the same explicit operation can be retried.
Normal **Download / Install** and **Restore previous** are refused while any
`rollback-*` entry exists, so a second operation cannot overwrite recovery
evidence. Ambiguous, untrusted, duplicate or invalid states are left untouched
for manual inspection. No GitHub request or Microsoft installer is involved.

**Repair install** handles a second case: interruption during a Lite
installation after the downloaded ZIP has passed integrity, extraction and
dependency checks. Before the first directory rename, the updater now writes
a small per-channel `install-transaction.json` journal identifying the
original current/previous versions and the target version. A separately
confirmed offline repair validates this journal, the selected channel,
staged folders and Lite x86 signatures, then:

- If promotion has not finished, restores the original managed installation
  and retains the staged candidate under `abandoned-<id>` for manual review.
- If the new version has already reached `current`, keeps it installed,
  verifies the previous backup and finishes safe housekeeping.
- Rejects ambiguous, missing, malicious or cross-channel evidence without
  guessing. Installation and rollback are blocked until the transaction
  has been repaired.

Recovery is repeatable if interrupted between recovery moves. The installer
keeps the journal until promotion is complete; a retired second backup is
cleaned up **after** the committed installation is safe. The journal does
not include user ECU profiles or settings. An unjournaled `pending-*` or
`older-*` from older Banner versions is deliberately **not** repaired
automatically; preserve those folders for manual analysis.

CI also includes eight isolated launch-candidate checks: valid/current-only
installations, absent/corrupt files, invalid x86 PE, wrong-channel metadata,
and both incomplete update and rollback. They do not run Dvinholm itself.

CI includes eight synthetic offline interrupted-install checks alongside
eight rollback-repair checks. Real GUI execution and power-cut recovery
still require Windows acceptance; these checks do not prove hardware-level
crash consistency.

During an ordinary update, the older `previous` backup is retained in a
temporary channel directory until the new `current` is successfully moved
into place. Failure before successful replacement restores the prior backup
where possible. Only one directly selectable prior build is retained after
a successful update, while cleanup failures can leave an extra `older-*`
folder for manual review. Rollback itself never downloads files or
installs dependencies. Windows PowerShell CI includes eight additional
synthetic offline rollback checks; installed Windows GUI and power-loss
recovery scenarios require manual acceptance.

## Windows acceptance (7 checks)

Use the latest **Development** standalone Banner ZIP from the GitHub
Development prerelease. Extract all three files, then run
`Updater-Banner.cmd` on the target Windows 11 / IoT LTSC machine.
A physical J2534 adapter is **not** needed. Do not create artificial
`pending-*` / `rollback-*` directories in a production installation.

1. **Standalone startup:** the WinForms Banner opens before .NET 10,
   Windows App SDK or VC++ x86 installation; initial local inventory runs
   without needing GitHub connectivity.
2. **Channel selection:** choose Stable, close/reopen, confirm Stable stays
   selected. Switch back to Development; both roots stay independent.
3. **Dependencies:** select **Recheck deps** offline, confirm the three
   statuses refresh; install a missing Microsoft runtime only after the
   confirmation prompt, then recheck.
4. **Availability:** use **Check updates** online. Confirm current and
   available labels reflect the selected channel.
5. **Lite installation:** confirm **Download / Install**; verify installed
   version and successful startup with **Start Dvinholm**. Do not
   interrupt Microsoft installation or an app process.
6. **Rollback:** after two different Development versions are installed,
   use **Restore previous**; confirm it works offline and that the
   replaced version remains in `previous`.
7. **Recovery indication:** use a disposable test profile or isolated test
   directory to simulate an interrupted operation. Confirm **Recovery**
   prompts for **Repair install** or **Repair rollback**, disables Install,
   Restore and Start, and refuses ambiguous states. Never delete recovery
   evidence until inspected.

CI verifies synthetic directory states, not a real power outage. It also
instantiates the **actual Windows PowerShell 5.1 WinForms controls** in a
noninteractive `-Mode UiSmokeTest` run and checks seven button-state
invariants (startup, ready, both repair modes, ambiguous recovery, busy
helper and construction). This smoke check never calls `ShowDialog`,
GitHub, Microsoft installers or Dvinholm; it does not prove visible GUI
rendering on a user's Windows desktop.

After packaging, Windows CI also extracts the **actual standalone ZIP** into
a temporary directory and runs seven checks against its shipped bytes:
exactly three expected files, successful extraction, SHA-256 source matches,
a local sibling launcher, offline self-tests, the WinForms smoke checks,
and read-only dependency inventory. The isolated directory is removed.
This guards against shipping an archive that matches the source at packaging
time but cannot execute as a standalone distribution. It still does **not**
substitute for manual Windows GUI acceptance or a real power-cut test. Report actual Windows checks
separately from CI and from physical J2534 tests.
Do not mark this acceptance complete until each check has been performed.

## Local recovery indication

The banner checks on-disk channel state **before** querying GitHub at
startup or after switching channels. `Recovery: Ready` enables normal
operations. `Recovery: Repair install` or `Repair rollback` enables
only the relevant confirmed recovery action. `Recovery: Inspect` blocks
install, rollback and launch; preserve the folders for manual inspection.
The status is advisory: the worker validates journal and file integrity
again before any repair. The GUI now updates button availability whenever a
**completed** worker-state snapshot is read, even if it arrives after the
process-exit notification. Intermediate progress snapshots may update text
but cannot unlock Install, Launch or Restore. Missing, malformed or
incomplete worker completion leaves these buttons disabled until a fresh
**Recheck deps**. Windows CI includes six simulated WinForms worker-status
ordering/validation checks in addition to the seven original button-state
checks. **Check updates** is an explicit online action
and is disabled while unresolved recovery evidence is present.

**Important:** the banner checks installation/availability, not physical
J2534 hardware support. A successful offline CI suite cannot prove actual
Windows GUI launch on the Panasonic Toughpad. The App SDK release line is
currently pinned to 2.5 to match Dvinholm. Updating this policy requires
a separate runtime/bootstrap compatibility review. If Windows PowerShell
5.1 or the inbox .NET Framework is disabled, this script-based banner
cannot launch; it is free of **additional** runtime prerequisites, not
free of Windows OS components.

Stable v0.2.4 remains immutable; this feature changes Development only.


## Launcher self-update: first safety gate (Development)

The independent native launcher can now **verify and stage**, but not yet
apply, a replacement from `Dvinholm-Updater-Banner-Windows-x86.zip`.
`Binaries/Assert-LauncherSelfUpdatePackage.ps1` requires a caller-provided
64-character SHA-256 obtained from independently validated release metadata.
It checks the whole ZIP before extraction, allows only the expected 11
files, rejects link/path aliases and Windows case-folding collisions, enforces
download and extraction limits, and validates the staged `Launcher.exe`
as a native Windows x86 PE32. The target staging folder must not exist.

The stage-only module makes **no changes to Launcher.exe or to the running
Binaries directory**, launches no archive code and cannot install drivers,
runtime packages, or applications. The future swapper must wait for the old
native launcher and its PowerShell GUI process to exit, revalidate the
staged bytes, use a crash-recoverable journal and preserve rollback.
It must never treat staging alone as a successful self-update.

CI runs eight offline fixture checks against the actual standalone ZIP,
including digest mismatch, traversal, duplicates, unexpected/missing files
and a tampered executable with a matching recalculated hash. Real Windows
self-update and restart acceptance remain pending.


## Launcher replacement transaction (offline, not yet enabled in UI)

The next Development safety gate provides
\`Binaries/Invoke-LauncherReplacement.ps1\` (Apply / Recover) as an isolated
post-exit worker. Apply requires an independently authenticated launcher ZIP
and SHA-256 plus the Launcher UI process ID; the worker waits for that
process to exit before changing the installed native Launcher.exe or support
files. It never changes Dvinholm.App.exe, application data or J2534 drivers.

The worker revalidates the original ZIP using the trusted packaged staging
validator, copies and hashes original launcher files into
\`.launcher-transaction/backup\`, writes a journal **before the first live
file change**, and installs replacement files through same-volume temporary
files. A successful update moves the complete verified backup to
\`.launcher-previous\`. An interrupted update retains its journal and can
restore originals with \`-Mode Recover\`; corrupt backups are rejected.
A pre-journal interruption can be discarded without touching live files.

This gate deliberately **does not yet** connect Apply to an automatic
download, provide a "restart updated Launcher" action, or prune/overwrite
an existing \`.launcher-previous\` snapshot. A second replacement is blocked
until a separate, tested rollback/backup-rotation workflow exists.
The helper must be launched as a trusted copy **outside** its installation
directory. Eight Windows CI tests exercise success, application isolation,
backup preservation, integrity failure, second-update refusal, deliberate
interruption, verified recovery and corrupted-backup refusal. Real Windows
process handoff and restart still require acceptance.


## Separate native Launcher version check (Development)

The Settings dialog includes **Check Launcher updates / Проверить обновление лаунчера**.
This invokes a read-only GitHub release metadata worker for the selected
Stable or Development channel. It does not download, unpack, replace, execute
or restart the native launcher. A publication missing the separate launcher
ZIP (or checksum manifest) is reported **Not published**, not as a usable
update. Invalid release assets are rejected.

A small `Binaries/launcher-build.txt` receipt, generated at packaging time,
identifies the native Launcher build separately from the selected application
build. Older launchers without the receipt report **Unverified**, and version
comparisons across Stable/Development never claim a safe automatic upgrade.
The native ZIP and combined Dvinholm ZIP carry the same receipt. Eight offline
comparison cases test current, newer, older, missing and mixed-channel states.

The journaled replacement helper remains offline and is **not yet wired** to
this button. Verified download, explicit confirmation, process-exit handoff,
restart and rollback-rotation are separate subsequent acceptance gates.


## Prepare native Launcher update (explicit, offline-safe boundary)

Settings now offers two separate bilingual native Launcher actions: check for
release metadata, and **Download & verify / Скачать и проверить**. The second
requires explicit user confirmation. The worker fetches the public, validated
GitHub standalone ZIP and SHA256SUMS only after confirmation. It checks both
asset sizes and API-provided SHA-256 digests, exactly one matching manifest
entry, the strict 15-file native package allowlist and the internal
launcher-build.txt receipt against the selected release tag.

Successful preparation atomically records a separate verified snapshot under
\`%LOCALAPPDATA%\Dvinholm\UpdaterBanner\LauncherUpdates\<channel>\prepared\`.
An existing prepared snapshot is never overwritten. A failed preparation
cleans its uniquely named partial download folder. Eight CI checks use the
actual packaged ZIP and checksum manifest, including tampering, incorrect
release identity, repeated manifest entries and preexisting staging.

**No live Launcher.exe, Binaries files, app, drivers or runtimes are changed
by preparation.** Applying the snapshot and restarting the launcher are still
separate safety gates, not yet exposed in the UI. These operations do not
perform physical J2534 testing.
