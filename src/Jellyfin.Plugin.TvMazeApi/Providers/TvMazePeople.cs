using Jellyfin.Data.Enums;
using Jellyfin.Plugin.TvMazeApi.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.TvMazeApi.Providers;

/// <summary>Maps TVmaze cast and crew to Jellyfin people.</summary>
internal static class TvMazePeople
{
    public static bool Enabled => Plugin.Instance?.Configuration.IncludePeople ?? true;

    public static IEnumerable<PersonInfo> FromCast(IEnumerable<TvMazeCastMember> cast, PersonKind kind)
    {
        foreach (var member in cast)
        {
            if (Create(member.Person, kind, member.Character?.Name) is { } person)
            {
                yield return person;
            }
        }
    }

    /// <summary>Series crew: creators and producers (TVmaze lists directors and writers per episode).</summary>
    public static IEnumerable<PersonInfo> FromCrew(IEnumerable<TvMazeCrewMember> crew)
    {
        foreach (var member in crew)
        {
            if (CrewKind(member.Type) is { } kind && Create(member.Person, kind, member.Type) is { } person)
            {
                yield return person;
            }
        }
    }

    /// <summary>Episode guest stars, directors and writers.</summary>
    public static IEnumerable<PersonInfo> FromEpisode(TvMazeEpisode episode)
    {
        if (episode.Embedded is not { } embedded)
        {
            yield break;
        }

        foreach (var person in FromCast(embedded.Guestcast, PersonKind.GuestStar))
        {
            yield return person;
        }

        foreach (var member in embedded.Guestcrew)
        {
            if (CrewKind(member.GuestCrewType) is { } kind && Create(member.Person, kind, member.GuestCrewType) is { } person)
            {
                yield return person;
            }
        }
    }

    /// <summary>Maps a TVmaze crew type to a Jellyfin person kind, or null for roles Jellyfin has no use for.</summary>
    public static PersonKind? CrewKind(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        if (type.Equals("Creator", StringComparison.OrdinalIgnoreCase)
            || type.Equals("Developer", StringComparison.OrdinalIgnoreCase))
        {
            return PersonKind.Creator;
        }

        if (type.Equals("Director", StringComparison.OrdinalIgnoreCase))
        {
            return PersonKind.Director;
        }

        if (type.Equals("Writer", StringComparison.OrdinalIgnoreCase)
            || type.Equals("Story", StringComparison.OrdinalIgnoreCase)
            || type.Equals("Teleplay", StringComparison.OrdinalIgnoreCase))
        {
            return PersonKind.Writer;
        }

        // "Executive Producer", "Co-Producer" … but not "Assistant To Producers".
        if (type.EndsWith("Producer", StringComparison.OrdinalIgnoreCase))
        {
            return PersonKind.Producer;
        }

        return null;
    }

    private static PersonInfo? Create(TvMazePerson? source, PersonKind kind, string? role)
    {
        if (string.IsNullOrWhiteSpace(source?.Name))
        {
            return null;
        }

        var person = new PersonInfo
        {
            Name = source.Name.Trim(),
            Role = role,
            Type = kind,
            ImageUrl = source.Image?.Original,
        };
        person.SetProviderId(TvMazeHelpers.ProviderKey, TvMazeHelpers.ToId(source.Id));
        return person;
    }
}
