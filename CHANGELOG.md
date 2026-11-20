# Changelog

## [1.0.0] - Unreleased

### Added
- Metadata providers for series, seasons and episodes from TVmaze, plus image providers for each.
- Rate limiter with a strict sliding window (20 calls/10 s), a global pause, and retries on HTTP 429/5xx.
- In-memory cache (60 min) that lets concurrent identical requests share one call.
- Settings page, external IDs and links back to TVmaze.
- English README, with the Danish version in README.da.md.
