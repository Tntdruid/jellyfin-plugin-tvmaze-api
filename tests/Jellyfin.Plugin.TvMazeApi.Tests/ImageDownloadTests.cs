using System.Net;
using Jellyfin.Plugin.TvMazeApi.Api;
using Jellyfin.Plugin.TvMazeApi.Configuration;
using Jellyfin.Plugin.TvMazeApi.Providers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Jellyfin.Plugin.TvMazeApi.Tests;

/// <summary>
/// Regression: the TVmaze CDN sometimes answers https image requests with a 302 to the same
/// http URL, which .NET refuses to follow ("Cannot fetch image … Http status code: Found").
/// </summary>
public class ImageDownloadTests
{
    private const string ImageUrl = "https://static.tvmaze.com/uploads/images/original_untouched/261/654573.jpg";

    private sealed class SequenceHandler(params Func<Uri, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            var respond = responses[Math.Min(Requests.Count - 1, responses.Length - 1)];
            return Task.FromResult(respond(request.RequestUri!));
        }
    }

    private static HttpResponseMessage RedirectToHttp(Uri uri)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new UriBuilder(uri) { Scheme = "http", Port = 80 }.Uri;
        return response;
    }

    private static HttpResponseMessage Image(Uri uri) => new(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };

    private static async Task<HttpResponseMessage> Download(TvMazeClient client, FakeTimeProvider time, string url)
    {
        var task = client.GetImageResponseAsync(url, CancellationToken.None);
        while (!task.IsCompleted)
        {
            await Task.Delay(1);
            time.Advance(TimeSpan.FromSeconds(1));
        }

        return await task;
    }

    private static TvMazeClient Client(HttpMessageHandler handler, TimeProvider time)
        => new(handler, () => new PluginConfiguration(), time, NullLogger<TvMazeClient>.Instance);

    [Fact]
    public async Task RetriesHttpsWhenCdnRedirects()
    {
        var time = new FakeTimeProvider();
        var handler = new SequenceHandler(RedirectToHttp, Image);
        using var client = Client(handler, time);

        using var response = await Download(client, time, ImageUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, u => Assert.Equal("https", u.Scheme));
    }

    [Fact]
    public async Task FollowsRedirectOnSameHostAfterRetries()
    {
        var time = new FakeTimeProvider();
        var handler = new SequenceHandler(RedirectToHttp, RedirectToHttp, RedirectToHttp, Image);
        using var client = Client(handler, time);

        using var response = await Download(client, time, ImageUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TvMazeClient.ImageRetries + 2, handler.Requests.Count);
        Assert.Equal("http", handler.Requests[^1].Scheme);
        Assert.Equal("static.tvmaze.com", handler.Requests[^1].Host);
    }

    [Fact]
    public async Task RefusesRedirectToAnotherHost()
    {
        var time = new FakeTimeProvider();
        var handler = new SequenceHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = new Uri("http://evil.example/x.jpg");
            return response;
        });
        using var client = Client(handler, time);

        await Assert.ThrowsAsync<HttpRequestException>(() => Download(client, time, ImageUrl));
    }

    [Fact]
    public async Task PersonImageProviderUsesTvMazePhoto()
    {
        var handler = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":42,"name":"Mike Vogel","image":{"medium":"m.jpg","original":"o.jpg"}}"""),
        });
        using var client = Client(handler, new FakeTimeProvider());
        var person = new Person();
        person.SetProviderId(MetadataProvider.TvMaze, "42");

        var images = (await new TvMazePersonImageProvider(client).GetImages(person, CancellationToken.None)).ToList();

        var image = Assert.Single(images);
        Assert.Equal("o.jpg", image.Url);
        Assert.Equal(ImageType.Primary, image.Type);
        Assert.Equal("/people/42", handler.Requests.Single().AbsolutePath);
    }
}
