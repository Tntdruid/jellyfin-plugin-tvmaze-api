# Changelog

## 1.2.1

- Fixed "Cannot fetch image … Http status code: Found". The TVmaze image CDN sometimes redirects https to http, which .NET won't follow. Images are now retried over https and only then follow the redirect, on the same host only.
- Person photos now come from a TVmaze image provider that uses this download, instead of bare image URLs that Jellyfin fetched on its own.

## 1.2.0

- Series now get creators and producers as well as the main cast.
- Episodes now get guest stars, directors and writers. One call per season covers every episode in it.
- New "Provide cast and crew" setting (on by default). Turn it off if another provider should supply people.

## 1.1.0

- Optional TVmaze account per Jellyfin user: episodes you finish or mark as played are marked as watched on TVmaze. Off by default; turn it on with the "Mark watched episodes on TVmaze (requires TVmaze premium)" checkbox.
- "Test login" button on the settings page that checks the username and API key and shows your premium level.
- Scheduled task "Sync watched episodes to TVmaze" that sends your existing watch history.
- Episodes are sent in batches through the same rate limiter as everything else.

## 1.0.1

- People now show a "TVmaze Person ID" field and a link to their TVmaze page.

## 1.0.0

- Metadata providers for series, seasons and episodes from TVmaze, plus image providers for each.
- Rate limiter with a strict sliding window (20 calls/10 s), a global pause, and retries on HTTP 429/5xx.
- In-memory cache (60 min) that lets concurrent identical requests share one call.
- Settings page, external IDs and links back to TVmaze.
- Installable from the plugin repository at `https://raw.githubusercontent.com/Tntdruid/jellyfin-plugin-tvmaze-api/main/manifest.json`.
