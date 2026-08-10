using System.Reflection;
using System.Text.Json;

namespace WilliamBelle.Monitoring;

/// <summary>
/// The packages this deployment restored, read from the dependency manifest the
/// runtime already writes beside the application.
///
/// The obvious source — the assemblies the CLR has loaded — answers a different
/// question, and answers it differently every run. Anything loaded lazily is
/// absent until something touches it, so a snapshot taken moments after startup
/// misses whatever the process has not needed yet, and two hosts on the
/// identical build disagree because they happened to serve different requests.
/// Most of what it does return is shared-framework assemblies, which the runtime
/// version already accounts for.
///
/// The dependency manifest is the restore's own answer: the third-party packages
/// this deployment shipped with, at the versions it shipped, complete before the
/// process has done anything. That is the list a repository's manifest can be
/// compared against, which is the only reason the agent reports packages at all.
/// Reading it needs nothing beyond the base class library, so the promise that
/// this package adds no supply-chain surface to someone else's application
/// survives.
/// </summary>
internal static class PackageInventory
{
    /// <summary>
    /// The restored packages, or the loaded assemblies where there is no
    /// manifest to read — single-file and ahead-of-time-compiled deployments
    /// have none, and a weaker inventory beats no snapshot at all.
    /// </summary>
    public static List<MonitoringSnapshot.PackageInfo> Read()
    {
        foreach (var path in ManifestPaths())
        {
            if (TryRead(path) is { Count: > 0 } packages) return packages;
        }

        return LoadedAssemblies();
    }

    /// <summary>
    /// Where the runtime says the manifests are. It records this while starting
    /// the application, so it stays right for published layouts that probing
    /// beside the entry assembly would not find. The fallback covers the hosts
    /// that leave it unset, some test runners among them.
    /// </summary>
    private static IEnumerable<string> ManifestPaths()
    {
        if (AppContext.GetData("APP_CONTEXT_DEPS_FILES") is string joined)
        {
            foreach (var path in joined.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                yield return path;
            }
        }

        if (Assembly.GetEntryAssembly()?.GetName().Name is { } entry)
        {
            yield return Path.Combine(AppContext.BaseDirectory, $"{entry}.deps.json");
        }
    }

    /// <returns><c>null</c> when the file is absent, unreadable or not the shape
    /// expected — every one of which means fall through to the next source
    /// rather than fail. Collecting a snapshot must not throw.</returns>
    private static List<MonitoringSnapshot.PackageInfo>? TryRead(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);

            if (!document.RootElement.TryGetProperty("libraries", out var libraries)
                || libraries.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var packages = new List<MonitoringSnapshot.PackageInfo>();
            foreach (var library in libraries.EnumerateObject())
            {
                // The type separates a restored package from the application's
                // own projects and from the runtime pack. Only a package can
                // drift against what a repository declares, and the application's
                // own assemblies are not something anyone can look up a
                // vulnerability for.
                if (!library.Value.TryGetProperty("type", out var type)
                    || type.ValueKind != JsonValueKind.String
                    || !string.Equals(type.GetString(), "package", StringComparison.Ordinal))
                {
                    continue;
                }

                // Keyed "Name/Version". Last separator, not first: a name cannot
                // contain one, but being explicit costs nothing.
                var separator = library.Name.LastIndexOf('/');
                if (separator <= 0 || separator == library.Name.Length - 1) continue;

                packages.Add(new MonitoringSnapshot.PackageInfo
                {
                    Name = library.Name[..separator],
                    Version = library.Name[(separator + 1)..],
                });
            }

            return Sorted(packages);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static List<MonitoringSnapshot.PackageInfo> LoadedAssemblies() =>
        Sorted(AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetName())
            .Where(n => n.Name is not null)
            .Select(n => new MonitoringSnapshot.PackageInfo
            {
                Name = n.Name!,
                Version = n.Version?.ToString() ?? "unknown",
            })
            .ToList());

    /// <summary>
    /// Ordinal, not the default culture-sensitive comparer: snapshots are
    /// compared across time and machines to spot drift, so the order must not
    /// depend on the host's locale. The Node agent sorts the same way for the
    /// same reason.
    /// </summary>
    private static List<MonitoringSnapshot.PackageInfo> Sorted(
        List<MonitoringSnapshot.PackageInfo> packages) =>
        packages
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ThenBy(p => p.Version, StringComparer.Ordinal)
            .ToList();
}
