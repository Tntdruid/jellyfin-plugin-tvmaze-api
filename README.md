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

Requires Jellyfin 12.2.

**From the plugin repository (recommended)**

1. In Jellyfin go to **Dashboard → Plugins → Repositories** (or *Catalog → ⚙️*) and add:
   ```
   https://raw.githubusercontent.com/Tntdruid/jellyfin-plugin-tvmaze-api/main/manifest.json
   ```
2. Open the **Catalog**, find **TVmaze API** and install it.
3. Restart Jellyfin.
4. In each TV library's settings, enable **TVmaze API** as a metadata and image provider.

**Manual**

1. Download the latest `.zip` from [Releases](https://github.com/Tntdruid/jellyfin-plugin-tvmaze-api/releases).
2. Unzip it into a folder named `TVmaze API_<version>` inside Jellyfin's `plugins` folder:
   - Windows: `C:\ProgramData\Jellyfin\Server\plugins\`
   - Linux: `/var/lib/jellyfin/plugins/`
   - Docker: `/config/plugins/`
3. Restart Jellyfin, then enable the provider as in step 4 above.

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

To release a new version, add a `## x.y.z` section to [CHANGELOG.md](CHANGELOG.md) and push a `vx.y.z` tag. The release workflow builds the zip, creates the GitHub release and adds the version to `manifest.json`.

## More Jellyfin projects

- [Episode Title Fixer](https://github.com/Tntdruid/jellyfin-plugin-episode-title-fixer): Jellyfin plugin that replaces placeholder episode titles like "Afsnit 11" with the original title.
- [Auto Artist Playlists](https://github.com/Tntdruid/jellyfin-plugin-auto-artist-playlists): Jellyfin plugin that hearts songs you listen to and adds them to a private playlist per artist.
- [TvFin](https://github.com/Tntdruid/TvFin): minimal Jellyfin player for Samsung Tizen TVs.
- [tonefin](https://github.com/Tntdruid/tonefin): small Windows music player for Jellyfin that always reports playback to the server.
- [Jellyfin Music Stats](https://github.com/Tntdruid/jellyfin-music-stats): Home Assistant add-on with music listening stats for Jellyfin.
- [Jellyfin Video Stats](https://github.com/Tntdruid/jellyfin-video-stats): Home Assistant add-on with viewing stats for movies, series and YouTube videos in Jellyfin.

## License

The code is licensed under GPL-3.0. Data from [TVmaze](https://www.tvmaze.com) is licensed under [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/). This project is not affiliated with TVmaze.
