using System.Net;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WilliamBelle.Monitoring;

/// <summary>
/// Collects a snapshot of the running application on a schedule and POSTs it,
/// HMAC-signed, to the ingest endpoint. Failures are logged and
/// swallowed — the sensor must never affect the host application.
/// </summary>
public sealed class MonitoringService(
    IOptions<MonitoringOptions> options,
    IHttpClientFactory httpClientFactory,
    IHostEnvironment environment,
    ILogger<MonitoringService> logger) : BackgroundService
{
    /// <summary>The named client this agent's requests go through.</summary>
    internal const string HttpClientName = "williambelle-monitoring";

    /// <summary>
    /// Reports on the configured interval until the host shuts down. Failures
    /// are logged and swallowed: monitoring must never take down the
    /// application it is monitoring.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReportAsync(opts, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Monitoring report failed; will retry next interval.");
            }
            await Task.Delay(opts.Interval, stoppingToken);
        }
    }

    private async Task ReportAsync(MonitoringOptions opts, CancellationToken ct)
    {
        var snapshot = Collect(opts.AppId, opts.EnvironmentName ?? environment.EnvironmentName);
        var json = JsonSerializer.Serialize(snapshot);
        var signature = PayloadSigner.Sign(json, opts.SigningKey);

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, opts.IngestUrl)
        {
            Content = JsonContent.Create(new { payload = json, signature }),
        };
        var response = await client.SendAsync(request, ct);

        // A refused signature and an unreachable endpoint have the same symptom
        // — snapshots stop arriving — and completely different remedies, so the
        // one case that is actionable from the host says what to do about it.
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            logger.LogWarning(
                "Monitoring snapshot refused: the ingest endpoint does not recognise this "
                + "application id, or the signing key configured here is not the one it holds. "
                + "A key rotated at the endpoint has to be updated here too.");
            return;
        }

        response.EnsureSuccessStatusCode();
        logger.LogInformation("Monitoring snapshot reported ({Count} packages).",
            snapshot.Packages.Count);
    }

    internal static MonitoringSnapshot Collect(string appId, string environmentName) => new()
    {
        AppId = appId,
        RuntimeVersion = RuntimeInformation.FrameworkDescription,
        EnvironmentName = environmentName,
        Packages = PackageInventory.Read(),
    };
}
