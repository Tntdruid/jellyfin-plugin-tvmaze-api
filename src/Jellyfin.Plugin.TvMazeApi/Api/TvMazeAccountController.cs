using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.TvMazeApi.Api;

/// <summary>Lets the settings page test a TVmaze login (the API key never leaves the server).</summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("TvMazeApi")]
[Produces(MediaTypeNames.Application.Json)]
public class TvMazeAccountController : ControllerBase
{
    private readonly TvMazeClient _client;

    public TvMazeAccountController(TvMazeClient client)
    {
        _client = client;
    }

    [HttpPost("Validate")]
    public async Task<ActionResult<ValidateResponse>> Validate([FromBody] ValidateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return BadRequest();
        }

        try
        {
            var premium = await _client.ValidateUserAsync(request.Username.Trim(), request.ApiKey.Trim(), cancellationToken).ConfigureAwait(false);
            return new ValidateResponse(premium is not null, premium ?? 0, null);
        }
        catch (HttpRequestException ex)
        {
            return new ValidateResponse(false, 0, ex.Message);
        }
    }

    public sealed record ValidateRequest(string Username, string ApiKey);

    /// <summary>Premium: 0 none, 1 Bronze, 2 Silver, 3 Gold.</summary>
    public sealed record ValidateResponse(bool Valid, int Premium, string? Error);
}
