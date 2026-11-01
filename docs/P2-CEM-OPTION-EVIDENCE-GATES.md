# XC90 P2 CEM: per-option evidence gates (Development)

The **P2 configuration candidates (offline only)** detail card now shows a
source-separated, six-point evidence checklist for each of the fourteen
existing structured configuration research labels. It consumes the immutable
`VolvoP2CemConfigOptionManifest`; it does not introduce a second catalog.

The first requirement deliberately differs between a third-party *advertised*
P2 feature category and a Volvo *published service-menu heading*. Neither is
proof that a particular XC90 supports a readable or writable setting.

For every label, the still-missing requirements are independently established
vehicle/model applicability, authenticated physical CEM hardware/application/
diagnostic software identity, actual module/equipment ownership, documented
revision-specific **read-only** mapping, validated allowed values and
dependencies, and backup/recovery plus authorized physical bench acceptance.

All six remain marked **MISSING**. Empty value/dependency collections are
unknown, never unrestricted. A source heading cannot become a CAN identifier,
DID, parameter offset, PIN, authorization route, physical ECU fingerprint or
supported configuration writer. Additional safety/equipment cautions are
retained when a source indicates them.

The detail view has one suggested *next research action* based on source tier
and never on user-supplied vehicle metadata. Invalid/private identifiers
return a generic message rather than echoing input.

Eight synthetic offline tests cover all fourteen labels, tier separation,
all missing gates, denied capabilities, untrusted identifiers, immutable
requirements and the existing dialog's output. No virtual-car repository
changes, actual J2534/UDS traffic, PIN retrieval, ECU configuration reads/
writes or physical acceptance. Stable v0.2.4 and its release assets remain
unchanged.
