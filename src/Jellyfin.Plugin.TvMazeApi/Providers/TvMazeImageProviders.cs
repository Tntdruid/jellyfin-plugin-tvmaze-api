using Jellyfin.Plugin.TvMazeApi.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.TvMazeApi.Providers;

public class TvMazeSeriesImageProvider : IRemoteImageProvider
{
    private readonly TvMazeClient _client;

    public TvMazeSeriesImageProvider(TvMazeClient client)
    {
        _client = client;
    }

    public string Name => Plugin.ProviderName;

    public bool Supports(BaseItem item) => item is Series;

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        => [ImageType.Primary, ImageType.Banner, ImageType.Backdrop, ImageType.Logo];

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var showId = await TvMazeHelpers.ResolveShowIdAsync(_client, item.ProviderIds, null, null, cancellationToken).ConfigureAwait(false);
        if (showId is null)
        {
            return [];
        }

        var images = await _client.GetShowImagesAsync(showId.Value, cancellationToken).ConfigureAwait(false);
        return MapImages(images);
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _client.GetImageResponseAsync(url, cancellationToken);

    internal static List<RemoteImageInfo> MapImages(IEnumerable<TvMazeShowImage> images)
    {
        var list = new List<RemoteImageInfo>();
        // Main images first so Jellyfin picks them by default.
        foreach (var image in images.OrderByDescending(i => i.Main))
        {
            ImageType? type = image.Type switch
            {
                "poster" => ImageType.Primary,
                "banner" => ImageType.Banner,
                "background" => ImageType.Backdrop,
                "typography" => ImageType.Logo,
                _ => null,
            };
            var original = image.Resolutions?.Original;
            if (type is null || string.IsNullOrEmpty(original?.Url))
            {
                continue;
            }

            list.Add(new RemoteImageInfo
            {
                ProviderName = Plugin.ProviderName,
                Type = type.Value,
                Url = original.Url,
                Width = original.Width,
                Height = original.Height,
                ThumbnailUrl = image.Resolutions?.Medium?.Url,
            });
        }

        return list;
    }
}

public class TvMazeSeasonImageProvider : IRemoteImageProvider
{
    private readonly TvMazeClient _client;

    public TvMazeSeasonImageProvider(TvMazeClient client)
    {
        _client = client;
    }

    public string Name => Plugin.ProviderName;

    public bool Supports(BaseItem item) => item is Season;

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => [ImageType.Primary];

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        if (item is not Season season || season.Series is null)
        {
            return [];
        }

        var found = await TvMazeSeasonProvider.FindSeasonAsync(_client, season.Series.ProviderIds, season.IndexNumber, cancellationToken)
            .ConfigureAwait(false);
        return ToPrimary(found?.Image);
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _client.GetImageResponseAsync(url, cancellationToken);

    internal static IEnumerable<RemoteImageInfo> ToPrimary(TvMazeImage? image)
        => string.IsNullOrEmpty(image?.Original)
            ? []
            : [new RemoteImageInfo { ProviderName = Plugin.ProviderName, Type = ImageType.Primary, Url = image.Original, ThumbnailUrl = image.Medium }];
}

public class TvMazeEpisodeImageProvider : IRemoteImageProvider
{
    private readonly TvMazeClient _client;

    public TvMazeEpisodeImageProvider(TvMazeClient client)
    {
        _client = client;
    }

    public string Name => Plugin.ProviderName;

    public bool Supports(BaseItem item) => item is Episode;

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => [ImageType.Primary];

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        if (item is not Episode episode || episode.Series is null)
        {
            return [];
        }

        var info = new EpisodeInfo
        {
            IndexNumber = episode.IndexNumber,
            ParentIndexNumber = episode.ParentIndexNumber,
            PremiereDate = episode.PremiereDate,
            ProviderIds = episode.ProviderIds,
            SeriesProviderIds = episode.Series.ProviderIds,
        };
        var found = await TvMazeEpisodeProvider.FindEpisodeAsync(_client, info, cancellationToken).ConfigureAwait(false);
        return TvMazeSeasonImageProvider.ToPrimary(found?.Image);
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _client.GetImageResponseAsync(url, cancellationToken);
}
