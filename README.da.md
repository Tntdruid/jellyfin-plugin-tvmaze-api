<p align="center">
  <img src="assets/banner.png" alt="TVmaze API – metadata-plugin til Jellyfin" width="720">
</p>

# TVmaze API til Jellyfin

🇬🇧 [English](README.md) · 🇩🇰 Dansk

Metadata-plugin til [Jellyfin](https://jellyfin.org), der henter serie-, sæson- og episodedata fra [TVmaze-API'et](https://www.tvmaze.com/api) — og overholder TVmaze' rate limit.

## Funktioner

- **Serier:** titel, beskrivelse, genrer, rating, premiere/slutdato, status, sendetid/-dage, netværk/streamingtjeneste, skuespillere og eksterne id'er (TheTVDB, IMDb, TVRage).
- **Sæsoner:** navn, beskrivelse, datoer og plakat.
- **Episoder:** titel, beskrivelse, sendedato, spilletid, rating og stillbillede. Specials (sæson 0) matches i sendefølge, og daglige shows kan matches på dato.
- **Billeder:** plakater, bannere, baggrunde og logoer.
- **Links** tilbage til TVmaze på hvert element.

## Sådan overholdes API-reglerne

| TVmaze-regel | Hvad pluginet gør |
|---|---|
| Mindst 20 kald pr. 10 sekunder pr. IP | Et strengt sliding window sender aldrig mere end 20 kald inden for 10 sekunder. Grænsen kan indstilles. |
| HTTP 429 betyder: vent lidt og prøv igen | Ved 429 pauses **alle** kald (med `Retry-After` eller eksponentiel backoff: 2, 4, 8 … op til 60 s), hvorefter kaldet prøves igen. Det samme gælder 5xx-fejl. |
| Hold ikke flere ubrugte forbindelser åbne | Én genbrugt forbindelse (HTTP/2, når det er muligt), som lukkes efter 30 sekunder uden brug. |
| Svar caches i 60 minutter | Svar caches i hukommelsen i 60 minutter. Ens samtidige forespørgsler deler ét kald, og fejl caches ikke. |
| Billeder kan caches/hotlinkes | Billeder hentes fra CDN'et uden om rate limiteren. |
| Angiv en User-Agent | `Jellyfin-Plugin-TvMazeApi/<version>` |
| CC BY-SA 4.0-kreditering | Hvert element linker til TVmaze, og indstillingssiden krediterer TVmaze. |

Pluginet bruger desuden få kald: episoder matches mod seriens samlede episodeliste (**ét** kald pr. serie), ikke ét kald pr. episode. En typisk serie koster derfor 4–6 kald i alt.

## Installation

Kræver Jellyfin 12.2.

**Fra plugin-repository (anbefalet)**

1. Gå til **Dashboard → Plugins → Repositories** (eller *Katalog → ⚙️*) i Jellyfin og tilføj:
   ```
   https://raw.githubusercontent.com/Tntdruid/jellyfin-plugin-tvmaze-api/main/manifest.json
   ```
2. Åbn **Katalog**, find **TVmaze API** og installér det.
3. Genstart Jellyfin.
4. Aktivér **TVmaze API** som metadata- og billedudbyder i indstillingerne for hvert tv-bibliotek.

**Manuelt**

1. Hent den nyeste `.zip` under [Releases](https://github.com/Tntdruid/jellyfin-plugin-tvmaze-api/releases).
2. Pak den ud i en mappe med navnet `TVmaze API_<version>` i Jellyfins `plugins`-mappe:
   - Windows: `C:\ProgramData\Jellyfin\Server\plugins\`
   - Linux: `/var/lib/jellyfin/plugins/`
   - Docker: `/config/plugins/`
3. Genstart Jellyfin, og aktivér udbyderen som i trin 4 ovenfor.

## Indstillinger

| Indstilling | Standard |
|---|---|
| Maks. kald pr. vindue | 20 |
| Vindue (sekunder) | 10 |
| Cache (minutter) | 60 |
| Maks. genforsøg ved 429 | 5 |

## Udvikling

```powershell
dotnet test
```

En ny version udgives ved at tilføje et afsnit `## x.y.z` i [CHANGELOG.md](CHANGELOG.md) og pushe et tag `vx.y.z`. Release-workflowet bygger zip-filen, laver GitHub-releasen og tilføjer versionen til `manifest.json`.

## Flere Jellyfin-projekter

- [Episode Title Fixer](https://github.com/Tntdruid/jellyfin-plugin-episode-title-fixer): Jellyfin-plugin, der erstatter pladsholdertitler som "Afsnit 11" med den originale titel.
- [Auto Artist Playlists](https://github.com/Tntdruid/jellyfin-plugin-auto-artist-playlists): Jellyfin-plugin, der giver sange, du lytter til, et hjerte og lægger dem på en privat playliste pr. kunstner.
- [TvFin](https://github.com/Tntdruid/TvFin): minimal Jellyfin-afspiller til Samsung Tizen-tv.
- [tonefin](https://github.com/Tntdruid/tonefin): lille Windows-musikafspiller til Jellyfin, der altid melder afspilning tilbage til serveren.
- [Jellyfin Music Stats](https://github.com/Tntdruid/jellyfin-music-stats): Home Assistant-add-on med lyttestatistik for musik i Jellyfin.
- [Jellyfin Video Stats](https://github.com/Tntdruid/jellyfin-video-stats): Home Assistant-add-on med seerstatistik for film, serier og YouTube-videoer i Jellyfin.

## Licens

Koden er licenseret under GPL-3.0. Data fra [TVmaze](https://www.tvmaze.com) er licenseret under [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/). Projektet er ikke tilknyttet TVmaze.
