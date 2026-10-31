# Native Windows x86 launcher RC icon path and warning gates

Development-only build hardening. The existing resource-icon path conversion was
already replacing each Windows backslash correctly. This follow-up extracts
that conversion to a small PowerShell 5.1 helper, rejects invalid quoted /
multiline icon paths before writing an RC string, and adds functional
regression checks. No launcher feature, vehicle protocol, or update logic is
changed.

- Resource icon paths are resolved before conversion and written with forward
  slashes to avoid RC string escape ambiguity, especially when directory
  segments start with n, r or t.
- Eight synthetic Windows PowerShell 5.1 checks cover ordinary drive paths,
  spaces, escape-looking directory names, Unicode, UNC, idempotent slash
  paths, embedded quotes, and blank/multiline inputs. The Unicode gate also
  round-trips an actual temporary .rc file (cleaned afterward).
- RC files are written as UTF-8 with `#pragma code_page(65001)`, so the
  Windows Resource Compiler does not silently decode non-ASCII icon paths
  using a system-dependent ANSI code page.
- Native launcher C++ builds now use MSVC /W4 and /WX. A compiler warning
  fails the CI build instead of being hidden by a successful PE/ZIP check.
- Real x86 compiler invocation, icon resource validation, PE architecture
  validation and final ZIP checks remain in the existing CI job.
- No installed app, user registry, external driver, J2534 hardware, CEM,
  ECU, or Stable release artifact is modified by these isolated tests.

Acceptance requires the exact PR-head Windows CI job to complete successfully.
A source-level inspection or eight passing helper cases alone is not proof of
a warning-free compiled Launcher.exe and Dvinholm.exe.
