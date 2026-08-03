using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prometheus;
using System.Net;

namespace SosuBot.ScoresObserver.Monitoring;

public sealed class MetricsServerHostedService(
    IOptions<MonitoringConfiguration> options,
    ILogger<MetricsServerHostedService> logger) : IHostedService, IDisposable
{
    private MetricServer? _server;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Prometheus metrics endpoint is disabled");
            return Task.CompletedTask;
        }

        _server = new MetricServer(port: options.Value.Port);
        try
        {
            _server.Start();
        }
        catch (HttpListenerException exception)
        {
            _server.Dispose();
            _server = null;
            throw new InvalidOperationException(
                $"Unable to start the ScoresObserver metrics endpoint on port {options.Value.Port}. " +
                "Choose a free port using Monitoring:Port or Monitoring__Port.",
                exception);
        }

        logger.LogInformation("Prometheus metrics endpoint listening on port {Port}", options.Value.Port);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_server is not null)
            await _server.StopAsync();
    }

    public void Dispose() => _server?.Dispose();
}
