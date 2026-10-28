# XC90 P2 CEM: distinguish published read surfaces from wire formats

**Development 0.3.0, offline research only.** This page records the human-facing
diagnostic functions described by a **MY2007 XC90 CEM** workshop manual
republication. It does not contain manufacturer protocol definitions and
does **not** assert general 2003–2014 applicability.

The existing **P2 configuration candidates (offline only)** → **Cross-check CEM
revision source JSON (offline)** action now appends a source-scoped read-surface
matrix, in addition to the five independent read-eligibility blockers.

| Source-facing category | What MY2007 reference says | Why this is not a byte format |
| --- | --- | --- |
| Module identity | VIDA displays hardware P/N, hardware serial, software P/N and diagnostic-software P/N | No service ID or wire encoding specified; serial numbers must not be copied into research fixtures |
| Live diagnostic parameters | VIDA can display current statuses/values | Not a configuration file or validated CEM read method |
| Frozen DTC context | Statuses and counters stored in relation to a DTC | Historical fault context, not live settings |
| Customer-programmed data | VIDA has a function for reading/programming customer parameters | No revision-specific request, response layout or memory map |
| Vehicle configuration file | CEM stores VIN, structure week and equipment | Different from customer parameters, contains private vehicle identity and is not a restorable dump |

**Evidence basis (MY2007 only):**
- [XC90 CEM Diagnostic Functions, workshop manual reproduction](https://lemon-manuals.org.ua/Volvo/2007/XC90%20FWD%20L6-3.2L%20VIN%2098%20B6324S/Repair%20and%20Diagnosis/Body%20and%20Frame/Body%20Control%20Systems/Description%20and%20Operation/Central%20Electronic%20Module%20%28CEM%29/Diagnostic%20Functions/)
- [XC90 CEM System Overview, workshop manual reproduction](https://lemon-manuals.org.ua/Volvo/2007/XC90%20FWD%20L6-3.2L%20VIN%2098%20B6324S/Repair%20and%20Diagnosis/Body%20and%20Frame/Body%20Control%20Systems/Description%20and%20Operation/Central%20Electronic%20Module%20%28CEM%29/System%20Overview/)

Both sources are third-party reproductions of workshop descriptions, not
authenticated live observations or permission to perform ECU operations.
**Known user-facing functions are not equivalent to known diagnostic wire
protocols.** Exact CEM hardware/software revisions, physical network and
addresses, documented read request/response encoding and lengths, negative
responses, trusted consent, physical acceptance and recovery remain missing.

The new `VolvoP2CemReadSurfaceEvidence` is an immutable five-entry reference,
not a UDS DID allowlist. For a claimed year other than 2007 it reports
`OutsideExactPublishedExampleYear`; this means the *citation's scope* is
unknown, **not** that the vehicle is incompatible. The current profile-based
UDS DID capture remains separate from CEM configuration and is not a
restorable backup. No CAN/ISO-TP/J2534 request, configuration writer or
security/PIN route was added.

Eight synthetic offline tests verify category separation, scope boundaries,
reference-only classification, data sensitivity, permanent fail-closed service
flags, invalid enum refusal and WinUI report wiring. Installed Windows UI
and physical hardware acceptance remain for later milestones. Stable
`v0.2.4` is unchanged.
