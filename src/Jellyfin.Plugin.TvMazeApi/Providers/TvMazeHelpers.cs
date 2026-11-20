using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.TvMazeApi.Api;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.TvMazeApi.Providers;

internal static partial class TvMazeHelpers
{
    public static readonly string ProviderKey = MetadataProvider.TvMaze.ToString();

    public static string? StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var text = ParagraphRegex().Replace(html, "\n\n");
        text = BreakRegex().Replace(text, "\n");
        text = TagRegex().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text).Trim();
        return text.Length == 0 ? null : text;
    }

    public static DateTime? ParseDate(string? value)
        => DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
            ? date
            : null;

    public static int? ParseInt(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;

    public static string ToId(int id) => id.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Finds the TVmaze show id: own id first, then TheTVDB, IMDb, TVRage lookup, finally name search.
    /// Lookups are cheap (cached, rate limited) and far more accurate than name search.
    /// </summary>
    public static async Task<int?> ResolveShowIdAsync(
        TvMazeClient client,
        IReadOnlyDictionary<string, string>? providerIds,
        string? name,
        int? year,
        CancellationToken ct)
    {
        if (providerIds is not null)
        {
            if (providerIds.TryGetValue(ProviderKey, out var own) && ParseInt(own) is { } ownId)
            {
                return ownId;
            }

            (string Key, string Source)[] lookups =
            [
                (MetadataProvider.Tvdb.ToString(), "thetvdb"),
                (MetadataProvider.Imdb.ToString(), "imdb"),
                (MetadataProvider.TvRage.ToString(), "tvrage"),
            ];
            foreach (var (key, source) in lookups)
            {
                if (providerIds.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                    && await client.LookupShowIdAsync(source, value, ct).ConfigureAwait(false) is { } found)
                {
                    return found;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var results = await client.SearchShowsAsync(name, ct).ConfigureAwait(false);
        return PickBestMatch(results, name, year)?.Id;
    }

    /// <summary>Prefers exact name + year, then exact name, then year, then TVmaze relevance order.</summary>
    public static TvMazeShow? PickBestMatch(IReadOnlyList<TvMazeSearchResult> results, string name, int? year)
    {
        var shows = results.Select(r => r.Show).OfType<TvMazeShow>().ToList();
        bool NameMatches(TvMazeShow s) => string.Equals(Normalize(s.Name), Normalize(name), StringComparison.OrdinalIgnoreCase);
        bool YearMatches(TvMazeShow s) => year is not null && ParseDate(s.Premiered)?.Year == year;

        return shows.FirstOrDefault(s => NameMatches(s) && YearMatches(s))
            ?? shows.FirstOrDefault(NameMatches)
            ?? shows.FirstOrDefault(YearMatches)
            ?? shows.FirstOrDefault();
    }

    private static string Normalize(string? value)
        => NonWordRegex().Replace(value ?? string.Empty, string.Empty);

    [GeneratedRegex(@"</p>\s*<p[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ParagraphRegex();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"[\W_]+")]
    private static partial Regex NonWordRegex();
}
