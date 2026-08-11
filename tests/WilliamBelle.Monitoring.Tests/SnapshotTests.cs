using System.Text.Json;
using WilliamBelle.Monitoring;

namespace WilliamBelle.Monitoring.Tests;

/// <summary>
/// What the agent collects is a promise made in the README. These tests hold
/// it to that promise: the snapshot describes the running process and nothing
/// about the people using it.
/// </summary>
public class SnapshotTests
{
    private static MonitoringSnapshot Collect() =>
        MonitoringService.Collect("test-app", "Production");

    [Fact]
    public void A_snapshot_describes_the_running_process()
    {
        var snapshot = Collect();

        Assert.Equal("test-app", snapshot.AppId);
        Assert.Equal("Production", snapshot.EnvironmentName);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.RuntimeVersion));
        Assert.NotEmpty(snapshot.Packages);
    }

    [Fact]
    public void Packages_are_reported_with_the_versions_deployed()
    {
        var snapshot = Collect();

        Assert.All(snapshot.Packages, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Name));
            Assert.False(string.IsNullOrWhiteSpace(p.Version));
        });

        // The point of the whole package: third-party dependencies as a
        // repository and an advisory database spell them. xunit is one this
        // test project restored, so it has to be in its own inventory.
        Assert.Contains(snapshot.Packages, p => p.Name == "xunit");
    }

    /// <summary>
    /// The agent's own assembly is a project reference here, so it is not a
    /// restored package and must not be reported as one. In a host application
    /// the same rule keeps the application's own assemblies out: nobody can
    /// look up an advisory for those, and a version nobody sets is noise in a
    /// column where a version is supposed to mean something.
    /// </summary>
    [Fact]
    public void The_applications_own_assemblies_are_not_reported_as_packages()
    {
        var snapshot = Collect();

        Assert.DoesNotContain(snapshot.Packages, p => p.Name.StartsWith("WilliamBelle."));
    }

    /// <summary>
    /// The shared framework is already accounted for by the runtime version, and
    /// reporting it once per assembly buried the handful of rows that matter.
    /// </summary>
    [Fact]
    public void The_shared_framework_is_not_reported_package_by_package()
    {
        var snapshot = Collect();

        Assert.DoesNotContain(snapshot.Packages, p => p.Name == "System.Runtime");
        Assert.DoesNotContain(snapshot.Packages, p => p.Name == "netstandard");
    }

    [Fact]
    public void Packages_are_ordered_so_snapshots_can_be_compared()
    {
        var keys = Collect().Packages.Select(p => (p.Name, p.Version)).ToList();

        Assert.Equal(
            keys.OrderBy(k => k.Name, StringComparer.Ordinal)
                .ThenBy(k => k.Version, StringComparer.Ordinal)
                .ToList(),
            keys);
    }

    [Fact]
    public void Collection_is_timestamped_in_utc()
        => Assert.Equal(TimeSpan.Zero, Collect().CollectedAt.Offset);

    [Fact]
    public void The_advisory_registry_is_named_the_way_that_registry_spells_it()
    {
        // Case matters to the lookup: "nuget" matches nothing, which would read
        // as an application with no known vulnerabilities.
        Assert.Equal("NuGet", Collect().Ecosystem);
    }

    /// <summary>
    /// The wire shape, pinned.
    ///
    /// The Node agent already has a test asserting it serializes these fields
    /// in this order "the way the .NET agent uses", and nothing on this side
    /// held that true. The two payloads are read side by side, and one client
    /// quietly gaining or reordering a field is the drift that costs an
    /// afternoon to spot.
    /// </summary>
    [Fact]
    public void The_payload_carries_these_fields_in_this_order()
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(Collect()));

        Assert.Equal(
            ["AppId", "RuntimeVersion", "EnvironmentName", "Packages", "Ecosystem", "CollectedAt"],
            document.RootElement.EnumerateObject().Select(p => p.Name));
    }

    /// <summary>
    /// The endpoint deserializes with default System.Text.Json options, which
    /// are case-sensitive. camelCase binds to nulls and comes back as a 400
    /// that looks like a malformed request.
    /// </summary>
    [Fact]
    public void Package_entries_are_pascal_case_and_carry_nothing_else()
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(Collect()));

        var packages = document.RootElement.GetProperty("Packages").EnumerateArray().ToList();
        Assert.NotEmpty(packages);
        Assert.All(packages, package => Assert.Equal(
            ["Name", "Version"], package.EnumerateObject().Select(p => p.Name)));
    }
}
