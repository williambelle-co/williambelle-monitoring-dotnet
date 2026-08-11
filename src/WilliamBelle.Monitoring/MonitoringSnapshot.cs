namespace WilliamBelle.Monitoring;

/// <summary>
/// Everything the sensor reports. This is the complete data surface: the
/// sensor is read-only and one-way — it accepts no inbound commands, no
/// remote configuration, and collects no logs or request payloads.
/// </summary>
public class MonitoringSnapshot
{
    /// <summary>The monitored application's id, issued by William Belle LLC.</summary>
    public required string AppId { get; set; }
    /// <summary>e.g. ".NET 10.0.4" — the runtime servicing level actually running.</summary>
    public required string RuntimeVersion { get; set; }
    /// <summary>ASPNETCORE_ENVIRONMENT as the process sees it — catches
    /// Development running in production.</summary>
    public required string EnvironmentName { get; set; }
    /// <summary>The third-party packages this deployment shipped with — what is
    /// actually deployed, as opposed to what the repository manifest says.</summary>
    public required List<PackageInfo> Packages { get; set; }

    /// <summary>
    /// Which advisory registry <see cref="Packages"/> belong to, spelled the way
    /// that registry spells it. Constant per agent.
    ///
    /// Sent rather than left to be inferred. The endpoint can read it out of
    /// <see cref="RuntimeVersion"/> instead, and does for agents older than
    /// this one, but that only holds for as long as both agents keep describing
    /// themselves the same way — and the wrong registry either misses a real
    /// advisory or matches an unrelated package that shares a name.
    ///
    /// Declared before <see cref="CollectedAt"/> on purpose: property order is
    /// the wire order, and the two agents' payloads are read side by side.
    /// </summary>
    public string Ecosystem { get; set; } = Ecosystems.NuGet;

    /// <summary>When the snapshot was taken, in UTC.</summary>
    public DateTimeOffset CollectedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// How an advisory database spells the registries these agents report from.
    /// Case matters to the lookup: "nuget" finds nothing, which would read as
    /// an application with nothing wrong.
    /// </summary>
    public static class Ecosystems
    {
        public const string NuGet = "NuGet";
    }

    /// <summary>One package and the version deployed.</summary>
    public class PackageInfo
    {
        /// <summary>Package id, as a repository or an advisory database spells it.</summary>
        public required string Name { get; set; }

        /// <summary>Version deployed, which may differ from the one the repository declares.</summary>
        public required string Version { get; set; }
    }
}
