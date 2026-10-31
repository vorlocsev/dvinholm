# CEM hardware / application / diagnostic-software revision triage (Development 0.3.0)

This feature extends the **existing two-file, offline CEM revision source cross-check**.
It does not identify or connect to a physical CEM.

## Purpose

The previous cross-check compared three strings but did not distinguish the
*next action needed* when a claimed hardware part number matched while the
application or diagnostic-software P/N differed. The new read-only triage
report makes these cases explicit, then optionally references the
non-exhaustive community hardware list introduced in PR #653.

### Outcomes and boundaries

- **Model year outside source scope:** no part-number comparison is accepted.
- **Different hardware P/N claims:** no common module/revision inferred, even
  if separate numbers are listed under the same third-party category.
- **Same hardware, different application software:** firmware compatibility
  remains unresolved.
- **Same hardware and application, different diagnostic software:** diagnostic
  software revision remains unresolved.
- **All three textual claims match:** classify community hardware number as
  a single mention, an ambiguous/multi-category mention, or absent from the
  selected source sample. The last case is *not* incompatibility.
- A number found in both third-party CEM-L and CEM-H remains ambiguous, never
  automatically resolved to a protocol, CAN ID or model-year fitment.

Both inputs remain editable **claims**. Even complete equality and one
community category do not authenticate an ECU, prove source independence,
verify firmware or imply the ability to read configuration, retrieve PIN or
write data.

## Existing workflow

Use the existing **CEM revision source cross-check** in the XC90 P2
research UI. Select a previously exported manual VIDA identity-research
card and a separate revision source claim. Their strict 4 KiB versioned
schemas and existing compare/inspection checks remain unchanged.

The original **SAME / DIFFERENT** report is preserved, with the new
hardware/application/diagnostic-software triage appended in the same dialog.
The additional report contains no actual P/N values, source reference
strings, file paths, VIN, PIN, raw DIDs or ECU bytes. It never saves or
copies without an existing user action.

## Implementation and verification

- `VolvoP2CemRevisionEvidenceTriage.Assess` reuses the existing strict
  `VolvoP2CemRevisionSourceComparison.Compare` validator before
  interpreting any part-number fields.
- It uses only the **selected, non-exhaustive** public community P/N sample
  from [vtl/volvo-cem-cracker, pinned source commit
  0130cd0](https://github.com/vtl/volvo-cem-cracker/blob/0130cd055da075823dd15a98e4a11d5496128e60/volvo-cem-cracker.ino).
  This is unverified research metadata, not a manufacturer compatibility
  dataset or a source of executable diagnostic behavior.
- Eight synthetic xUnit checks cover year exclusion, hardware/app/diagnostic
  mismatches, a single community mention, a duplicate category, an absent
  P/N, forged capabilities and report redaction.
- All live CEM capabilities remain disabled. No new J2534 access, CAN
  communication, PID/DID commands, PIN logic, ECU writes, file schema
  migrations or persistence.
- Windows installed UI and physical DiCE/J2534/CEM acceptance are still
  unperformed. Stable v0.2.4 and Virtual Car are unchanged.
