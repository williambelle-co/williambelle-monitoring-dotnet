using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace WilliamBelle.Monitoring;

/// <summary>Registration for the monitoring agent.</summary>
public static class ServiceCollectionExtensions
{
    private const string Prefix = "WilliamBelle.Monitoring:";

    /// <summary>
    /// Adds monitoring to an application:
    /// <code>
    /// builder.Services.AddWilliamBelleMonitoring(o =>
    /// {
    ///     o.IngestUrl = builder.Configuration["Monitoring:IngestUrl"]!;
    ///     o.AppId = builder.Configuration["Monitoring:AppId"]!;
    ///     o.SigningKey = builder.Configuration["Monitoring:SigningKey"]!;
    /// });
    /// </code>
    /// Prefer <see cref="TryAddWilliamBelleMonitoring"/> where the values come
    /// from configuration that may not carry them, such as a developer machine.
    /// </summary>
    /// <exception cref="ArgumentException">If any option is missing or malformed.</exception>
    public static IServiceCollection AddWilliamBelleMonitoring(
        this IServiceCollection services, Action<MonitoringOptions> configure)
    {
        var probe = new MonitoringOptions { IngestUrl = "", AppId = "", SigningKey = "" };
        configure(probe);
        Validate(probe);

        services.Configure(configure);
        services.AddHttpClient(MonitoringService.HttpClientName)
            .ConfigureHttpClient((provider, client) =>
                client.Timeout = provider.GetRequiredService<IOptions<MonitoringOptions>>().Value.Timeout);
        services.AddHostedService<MonitoringService>();
        return services;
    }

    /// <summary>
    /// Adds monitoring from a configuration section, and reports whether it did:
    /// <code>
    /// builder.Services.TryAddWilliamBelleMonitoring(builder.Configuration.GetSection("Monitoring"));
    /// </code>
    /// <para>
    /// An application is monitored in production and usually not on a developer
    /// machine, so "the values are absent, run without the agent" is the normal
    /// case rather than an error — every host would otherwise write the same
    /// three-key guard. A section holding <em>some</em> of them is a different
    /// thing: that is a half-finished deployment, and it throws.
    /// </para>
    /// Reads <c>IngestUrl</c>, <c>AppId</c>, <c>SigningKey</c>, and optionally
    /// <c>Interval</c>, <c>Timeout</c> and <c>EnvironmentName</c>.
    /// </summary>
    /// <returns><c>true</c> if the agent was registered.</returns>
    /// <exception cref="ArgumentException">If the section is partly filled in, or any value is malformed.</exception>
    public static bool TryAddWilliamBelleMonitoring(
        this IServiceCollection services, IConfiguration section)
    {
        ArgumentNullException.ThrowIfNull(section);

        var ingestUrl = section["IngestUrl"];
        var appId = section["AppId"];
        var signingKey = section["SigningKey"];

        if (string.IsNullOrWhiteSpace(ingestUrl)
            && string.IsNullOrWhiteSpace(appId)
            && string.IsNullOrWhiteSpace(signingKey))
        {
            return false;
        }

        services.AddWilliamBelleMonitoring(o =>
        {
            o.IngestUrl = ingestUrl ?? "";
            o.AppId = appId ?? "";
            o.SigningKey = signingKey ?? "";
            if (Duration(section, "Interval") is { } interval) o.Interval = interval;
            if (Duration(section, "Timeout") is { } timeout) o.Timeout = timeout;
            if (!string.IsNullOrWhiteSpace(section["EnvironmentName"]))
                o.EnvironmentName = section["EnvironmentName"];
        });
        return true;
    }

    /// <summary>
    /// Read by hand rather than through the configuration binder, which is a
    /// package this one would otherwise not need.
    /// </summary>
    private static TimeSpan? Duration(IConfiguration section, string key)
    {
        var raw = section[key];
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!TimeSpan.TryParse(raw, out var value))
        {
            throw new ArgumentException(
                $"{Prefix} {key} is not a timespan: '{raw}'. Use a value like 1.00:00:00.", nameof(section));
        }
        return value;
    }

    /// <summary>
    /// Fails at registration, where the stack trace names the application that
    /// misconfigured the agent, rather than a day later inside a background
    /// service where it would be a warning nobody reads.
    /// </summary>
    private static void Validate(MonitoringOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.IngestUrl))
            throw new ArgumentException($"{Prefix} IngestUrl is required.", nameof(options));

        if (!Uri.TryCreate(options.IngestUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                $"{Prefix} IngestUrl must be an absolute http or https address, got '{options.IngestUrl}'.",
                nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.AppId))
            throw new ArgumentException($"{Prefix} AppId is required.", nameof(options));

        // The endpoint parses this with Guid.TryParse and answers 400, not 401,
        // so a malformed id looks like a broken payload rather than an
        // authorization problem. Catching it here saves that diagnosis.
        if (!Guid.TryParse(options.AppId, out _))
        {
            throw new ArgumentException(
                $"{Prefix} AppId must be the GUID issued by William Belle LLC, got '{options.AppId}'.",
                nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.SigningKey))
            throw new ArgumentException($"{Prefix} SigningKey is required.", nameof(options));

        if (options.Interval <= TimeSpan.Zero)
            throw new ArgumentException($"{Prefix} Interval must be positive.", nameof(options));

        if (options.Timeout <= TimeSpan.Zero)
            throw new ArgumentException($"{Prefix} Timeout must be positive.", nameof(options));
    }
}
