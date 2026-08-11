using System.Net;

namespace SosuBot.Services;

/// <summary>
/// Marks transport and 5xx failures from the osu! API with a stable exception
/// type so the update handler can show a useful message to the user.
/// </summary>
public sealed class OsuApiAvailabilityHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            if ((int)response.StatusCode >= 500)
            {
                HttpStatusCode statusCode = response.StatusCode;
                response.Dispose();
                throw new OsuApiUnavailableException(
                    $"osu! API returned {(int)statusCode} ({statusCode}).");
            }

            return response;
        }
        catch (OsuApiUnavailableException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new OsuApiUnavailableException("The osu! API could not be reached.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OsuApiUnavailableException("The osu! API request timed out.", exception);
        }
    }
}

public sealed class OsuApiUnavailableException : HttpRequestException
{
    public OsuApiUnavailableException(string message)
        : base(message)
    {
    }

    public OsuApiUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

