<p align="center">
  <img src="assets/banner.png" alt="TVmaze API – metadata plugin for Jellyfin" width="720">
</p>

# TVmaze API for Jellyfin

🇬🇧 English · 🇩🇰 [Dansk](README.da.md)

A metadata plugin for [Jellyfin](https://jellyfin.org) that gets series, season and episode data from the [TVmaze API](https://www.tvmaze.com/api) and stays within TVmaze's rate limit.

## Features

- **Series:** title, overview, genres, rating, premiere and end dates, status, air time and air days, network or streaming service, cast, and external IDs (TheTVDB, IMDb, TVRage).
- **Seasons:** name, overview, dates and poster.
- **Episodes:** title, overview, air date, runtime, rating and still image. Specials (season 0) are matched in airing order, and daily shows can be matched by date.
- **Images:** posters, banners, backgrounds and logos.
- **Links** back to TVmaze on every item.

## How the API rules are followed

| TVmaze rule | What the plugin does |
|---|---|
| At least 20 calls per 10 seconds per IP | A strict sliding window never sends more than 20 calls in any 10-second span. The limit can be changed in the settings. |
| HTTP 429 means: wait a bit and retry | On a 429, **all** calls pause (using `Retry-After`, or exponential backoff of 2, 4, 8 … up to 60 s), then the call is retried. 5xx errors are handled the same way. |
| Don't keep several idle connections open | One reused connection (HTTP/2 when available), closed after 30 seconds without use. |
| Responses are cached for 60 minutes | Responses are cached in memory for 60 minutes. Identical concurrent requests share one call, and errors are never cached. |
| Images may be cached or hotlinked | Images are fetched from the CDN outside the rate limiter. |
| Send a User-Agent | `Jellyfin-Plugin-TvMazeApi/<version>` |
| CC BY-SA 4.0 attribution | Every item links to TVmaze, and the settings page credits TVmaze. |

The plugin also makes few calls. Episodes are matched against the series' full episode list, which is **one** call per series, not one call per episode. A typical series costs 4–6 calls in total.

## Installation

1. Build: `dotnet publish src/Jellyfin.Plugin.TvMazeApi -c Release -o out`
2. Copy `out/Jellyfin.Plugin.TvMazeApi.dll` to `<jellyfin-data>/plugins/TVmaze API_1.0.0.0/`
3. Restart Jellyfin, then enable **TVmaze API** as a metadata and image provider in your library settings.

Requires Jellyfin 12.2 / .NET 10.

## Settings

| Setting | Default |
|---|---|
| Max calls per window | 20 |
| Window (seconds) | 10 |
| Cache (minutes) | 60 |
| Max retries on 429 | 5 |

## Development

```powershell
dotnet test
```

## License

The code is licensed under GPL-3.0. Data from [TVmaze](https://www.tvmaze.com) is licensed under [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/). This project is not affiliated with TVmaze.
