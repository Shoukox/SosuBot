using System.Diagnostics;

namespace SosuBot.Monitoring;

public sealed class OutboundHttpMetricsHandler(BotMetrics metrics, string clientName) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using IDisposable inFlight = metrics.TrackHttpRequest(clientName);
        long startedAt = Stopwatch.GetTimestamp();
        string status = "exception";

        try
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            status = ((int)response.StatusCode).ToString();
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            status = "cancelled";
            throw;
        }
        catch (OperationCanceledException)
        {
            status = "timeout";
            throw;
        }
        finally
        {
            metrics.RecordHttpRequest(clientName, request, status, Stopwatch.GetElapsedTime(startedAt).TotalSeconds);
        }
    }
}
