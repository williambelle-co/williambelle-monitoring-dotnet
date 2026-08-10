namespace WilliamBelle.Monitoring;

/// <summary>
/// Where to report to, and as whom. The application id and signing key are
/// issued by William Belle LLC; keep the key in configuration or a secret
/// store, never in source.
///
/// These are validated when the agent is registered, not when it first reports.
/// A misconfiguration is the host developer's bug and is worth surfacing at
/// startup; everything that can go wrong afterwards is the endpoint's problem
/// and is caught and swallowed, because the agent must never take down the
/// application it is watching.
/// </summary>
public class MonitoringOptions
{
    /// <summary>Ingest endpoint, e.g. https://portal.williambelle.co/ingest/sensor.</summary>
    public required string IngestUrl { get; set; }
    /// <summary>The monitored app's id, issued by William Belle LLC. A GUID.</summary>
    public required string AppId { get; set; }
    /// <summary>Per-app signing key, issued by William Belle LLC. Store in Key Vault
    /// or environment configuration, never in source.</summary>
    public required string SigningKey { get; set; }
    /// <summary>How often a snapshot is reported. Default: 24 hours.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Per-request timeout. Default: 30 seconds, matching the Node agent.
    /// Without one the handler's default applies, which is long enough that a
    /// black-holed endpoint keeps a request open for most of an hour.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Overrides the environment name the host reports. Left unset — the usual
    /// case — the agent reports what <c>IHostEnvironment</c> says, which is the
    /// value whose disagreement with reality this field exists to catch. Set it
    /// only where the host's own notion of environment is not the one that
    /// matters.
    /// </summary>
    public string? EnvironmentName { get; set; }
}
