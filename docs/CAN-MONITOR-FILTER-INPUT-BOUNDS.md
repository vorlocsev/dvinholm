# CAN Monitor software filter input limits

Development follow-up for the display-only CAN Monitor filter.

Before this change the filter limited the list to 16 CAN IDs but called
`Trim()`, `Split(',')` and parsed each token without an overall pasted-text
length limit. Excessively long input could waste UI-thread work even though it
was not a valid CAN ID.

## Rules

- Reject an input longer than **256 characters before trimming/splitting**.
- Accept at most **16 comma-separated IDs**, unchanged.
- Each ID has **1 to 8 hex digits** after an optional case-insensitive `0x`
  prefix. Reject more digits even when extra digits are leading zeroes.
- Preserve 11-bit and 29-bit numeric matching, canonical zero-padded frame IDs,
  and all 29-bit arbitration IDs at or below `0x1FFFFFFF`.
- A normal empty or whitespace-only filter is still an unfiltered view.
  An oversized all-whitespace paste is rejected rather than treated as a wildcard.
- Invalid filters display no matching rows; buffered RX history, counts and
  underlying subscription are unchanged. This is not a J2534 hardware filter.

Eight offline xUnit scenarios exercise pasted length, oversized whitespace,
nine-digit leading zeros, eight-digit compatible matching, the full 16-ID
list, the 29-bit boundary, malformed buffered IDs, and virtual J2534 RX with
zero TX after invalid input. There are no vehicle-specific CEM addresses,
PIN operations, writes, physical J2534 tests or Stable release changes.

Actual responsiveness and interaction in the installed WinUI app remain
separate manual acceptance items. A green CI result validates the software
behavior, not physical adapter operation.
