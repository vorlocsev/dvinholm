# Synthetic ECU identity evidence catalog boundary

Development 0.3.0. The generic read-only identity matcher processes a
catalog of independently researched DID signatures. The existing P2/CEM
catalog validator already limited its entries, but the lower-level matcher
could be called directly with an unbounded enumerable or a mixture of valid
and malformed rules. Skipping invalid entries could cause an apparently
unique identity result after conflicting evidence was discarded.

The matcher now limits direct input to **128 rules** (examining at most 129),
requires the **entire** catalog to pass structural eligibility, and rejects
noncanonical CAN addressing and excessively long/control-bearing attribution.
An invalid or oversized catalog results in UNKNOWN, not a match. Observed
snapshots also need valid addressing and at most 128 DIDs.

An individual matching synthetic rule still yields an explicitly **read-only
identity classification**, not authorization. Multiple matching rules remain
AMBIGUOUS. No Volvo P2/CEM hardware/software signature, real diagnostic CAN
address, verified DID definition, PIN decoding, configuration writing or
service permission has been introduced. Production CEM catalog stays EMPTY.

Eight deterministic tests cover single/empty/maximum/overflow catalogs,
nonmatching invalid entries, malformed addresses/source strings, invalid
snapshot structure and genuine ambiguity. These are offline tests only;
installed Windows and real DiCE/J2534/CEM acceptance remain outstanding.
Stable v0.2.4 is unchanged.
