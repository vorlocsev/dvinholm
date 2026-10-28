# Standalone Dvinholm Updater Banner

A separate lightweight GUI for Windows 11 and Windows 11 IoT LTSC.
It works **before** installing Dvinholm Lite, .NET 10 Desktop,
Windows App SDK or VC++ x86. The only prerequisites for the banner
itself are the Windows-provided PowerShell 5.1 and .NET Framework
WinForms. It uses no third-party PowerShell module.

Download **Dvinholm-Updater-Banner-Windows-x86.zip** from the current
Development ci-N prerelease. Extract the three files into their own
directory and double-click **Updater-Banner.cmd**.

The banner provides a **Stable / Development** selector, installed and
available versions, three dependency statuses, a check button, a
separately confirmed **Download / Install** button and three independent
Microsoft installer/update buttons. Downloads are only started after
the user clicks and confirms; Windows UAC and vendor setup UI remain visible.

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
- No scheduled or unattended install; no elevated or silent commands.

Managed installation folders are distinct:

    %LOCALAPPDATA%\Dvinholm\UpdaterBanner\Stable\current
    %LOCALAPPDATA%\Dvinholm\UpdaterBanner\Development\current

A successful update leaves one prior version in the channel's
previous directory. Existing manual Dvinholm installations are **not**
modified automatically. This prevents accidental downgrades when switching
channels. Application ECU profiles and settings remain in the existing
separate user-data location. The banner's Start button launches the
selected Lite build via its dependency-aware startup command.

**Important:** the banner checks installation/availability, not physical
J2534 hardware support. A successful offline CI suite cannot prove actual
Windows GUI launch on the Panasonic Toughpad. The App SDK release line is
currently pinned to 2.5 to match Dvinholm. Updating this policy requires
a separate runtime/bootstrap compatibility review. If Windows PowerShell
5.1 or the inbox .NET Framework is disabled, this script-based banner
cannot launch; it is free of **additional** runtime prerequisites, not
free of Windows OS components.

Stable v0.2.4 remains immutable; this feature changes Development only.
