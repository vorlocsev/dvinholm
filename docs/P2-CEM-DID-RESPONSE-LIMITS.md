# Classic CAN read-only DID response limit

Development 0.3.0. Generic Dvinholm UDS ReadDataByIdentifier (0x22)
uses a Classic CAN ISO-TP channel with a 4095-byte maximum payload.

A positive 0x22 response consumes one SID (0x62) and two echoed DID
bytes, leaving a maximum of 4092 value bytes. The readback layer now
enforces that ceiling before hexadecimal encoding, including when an
injected test/future transport reader bypasses the ISO-TP wire decoder.

An oversized response aborts the complete snapshot. It cannot yield a
partial baseline. The existing cancellation and echoed DID checks still
apply. Eight synthetic tests cover a short reply, exact limit, limit+1,
huge injection, second-DID failure, wrong DID echo, cancellation and
explicit-consent export/strict reinspection at the limit.

All values remain opaque and potentially sensitive. A saved JSON is a
diagnostic snapshot, NOT a recoverable configuration backup. No Dvinholm
CEM PIN search, request/response map, vehicle variant, config decoder
or configuration write has been established by these checks.

Manual Windows acceptance and real DiCE/J2534/CEM checks are separate.
Stable v0.2.4 is unchanged. No release is authorized by this change.
