# XC90 P2 CEM: evidence-oriented candidate detail (Development)

The **P2 configuration candidates (offline only)** dialog now offers a
source-scoped detail view for any of its 14 research labels. This makes the
existing catalog easier to interpret without implying ECU compatibility.

## What the UI shows

After entering a filter or choosing an evidence tier, select a matching
research label from the new dropdown. The detail is divided into:

1. **What the source establishes:** an advertised feature category for
   eight third-party claims, or a published Volvo XC90 CEM service-menu
   label for six manufacturer-document categories. Evidence tiers never
   merge into proof of a supported setting.
2. **Unknowns:** physical CEM identity, firmware, equipment ownership,
   current value, value range/encoding, transport, read mapping and
   backup/recovery are explicitly unverified.
3. **Available operations:** reading these static descriptions is possible
   offline; CEM read, PIN/authorization, editing and writing remain blocked.
4. **Next evidence:** independently validated CEM revision, documented
   revision-scoped read-only behavior and authorized bench acceptance.

The source text is drawn from the already-owned fixed catalogs. The
selected ID must match an exact, case-sensitive Dvinholm research label,
and must belong to the currently selected evidence tier. Invalid
identifiers are not echoed or persisted.

Search results are now folded into a separate collapsed disclosure area;
the previous full historical notes remain under the existing disclosure.
Changing the query or the evidence-tier selector **clears the selected
detail** so that a stale result cannot be mistaken for a current match.

## Acceptance

Eight offline tests cover all 8 vendor labels, all 6 Volvo menu labels,
advertised year scope, safety and equipment review flags, strict tier
separation, invalid/private labels, disabled operations, and UI
selection/invalidation wiring.

No vehicle status or CEM parameter is read, inferred or set. There are
no new CAN/J2534/UDS calls, PIN algorithms, authentication procedures,
diagnostic mappings, backups or write capability. This is not a
Windows manual GUI acceptance test. VirtualCar and the frozen Stable
v0.2.4 release remain untouched.
