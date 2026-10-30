# Development CI warning cleanup

Scope: the seven code/analyzer warnings observed in CI #4312 / #4313. This is a warning-only cleanup, not a change to CAN, J2534, PIN or CEM configuration behavior.

## Checks and changes

1. `CS8620` in `VolvoP2ConfigDependencyResearch`: explicitly recognize that the existing `leftKnown && rightKnown && relationKnown` guard has already rejected null and unknown identifiers before inserting their pair into the relationship set. Nullability assertions are confined to that guarded branch; **no fallback default ID** is introduced.
2. Add an offline regression asserting that supplied records with null candidate IDs and null claim entries still report `UnknownCandidateId`, zero related claims, and no vehicle operations.
3. `CS0219`: remove a single unused local SHA from a build-status test, retaining the SHA actually checked by the format assertion.
4. `xUnit2031`: replace `Where().Single()` with the predicate overload of `Assert.Single`, retaining the one-match guarantee.
5. `xUnit2029`: assert directly that there are no forbidden scroll controls using predicate `Assert.DoesNotContain`.
6. `xUnit2020` (two sites): use `Assert.Fail` when early external updater processes exit, preserving their output text in failures.
7. `xUnit2004`: use `Assert.False` for a negative CEM research report text check.

## Acceptance

- Exact-head PR Windows x86 CI build, active tests, Stable smoke, Launcher ZIP checks.
- Full main CI after merge, including archived MSIX tests.
- Confirm that the seven code/analyzer warnings no longer appear in the relevant build log; do **not** suppress warnings globally.
- Node.js 20 deprecation from `ilammy/msvc-dev-cmd@v1` is intentionally **outside** this PR; requires separate MSVC toolchain migration and Windows x86 Launcher checks.

Published Stable v0.2.4 and VirtualCar are unchanged. No physical J2534 tests claimed.
