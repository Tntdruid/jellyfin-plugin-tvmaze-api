# Changelog

## [1.0.0] - Unreleased

### Tilføjet
- Metadata-udbydere for serier, sæsoner og episoder fra TVmaze, plus billedudbydere for samme.
- Rate limiter med strengt sliding window (20 kald/10 s), global pause og retry ved HTTP 429/5xx.
- In-memory-cache (60 min) med deling af samtidige forespørgsler.
- Indstillingsside, eksterne id'er og links til TVmaze.
