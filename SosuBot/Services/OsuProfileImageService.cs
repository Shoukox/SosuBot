using Microsoft.Extensions.Logging;
using OsuApi.BanchoV2.Users.Models;

namespace SosuBot.Services;

/// <summary>
///     Downloads a player's profile images from the URLs in their osu! profile: the avatar (<c>avatar_url</c>)
///     and the profile banner (<c>cover_url</c>). Shared by /osucard and the /userw widget.
/// </summary>
public sealed class OsuProfileImageService(IHttpClientFactory httpClientFactory, ILogger<OsuProfileImageService> logger)
{
    private const int MaximumImageBytes = 5 * 1024 * 1024;
    private static readonly Uri OsuBaseUri = new("https://osu.ppy.sh");

    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("CustomHttpClient");

    public Task<byte[]?> DownloadAvatarAsync(User user, CancellationToken cancellationToken = default) =>
        DownloadAsync(user.AvatarUrl, cancellationToken);

    public Task<byte[]?> DownloadBannerAsync(UserExtend user, CancellationToken cancellationToken = default) =>
        DownloadAsync(user.CoverUrl, cancellationToken);

    /// <summary>The image bytes, or <c>null</c> if the URL is missing, too large or unavailable.</summary>
    private async Task<byte[]?> DownloadAsync(string? imageUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri? uri))
            uri = new Uri(OsuBaseUri, imageUrl);

        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(uri,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaximumImageBytes)
                return null;

            byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return content.Length <= MaximumImageBytes ? content : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not download osu! profile image from {ImageUrl}", uri);
            return null;
        }
    }
}
