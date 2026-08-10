using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WilliamBelle.Monitoring.Tests;

/// <summary>
/// Registration is where a misconfiguration is still cheap to fix. These tests
/// pin the one judgement call in it: absent configuration is a deliberate "not
/// monitored here", and half-present configuration is a bug.
/// </summary>
public class RegistrationTests
{
    private const string AppId = "3f1b2c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d";
    private const string Key = "cff7b3b387213835227e93edad864e14373569f8875be162e77599a8fccbb9bb";

    private static IConfiguration Section(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void An_empty_section_leaves_the_application_unmonitored()
    {
        var services = new ServiceCollection();

        Assert.False(services.TryAddWilliamBelleMonitoring(Section()));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void A_complete_section_registers_the_agent()
    {
        var services = new ServiceCollection();

        Assert.True(services.TryAddWilliamBelleMonitoring(Section(
            ("IngestUrl", "https://portal.williambelle.co/ingest/sensor"),
            ("AppId", AppId),
            ("SigningKey", Key))));
        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService));
    }

    /// <summary>
    /// The failure this exists to prevent: a deployment that carries the address
    /// and the id but never got the key, which would otherwise start, report
    /// nothing, and look monitored.
    /// </summary>
    [Fact]
    public void A_half_filled_section_is_a_deployment_mistake_and_throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.TryAddWilliamBelleMonitoring(Section(
            ("IngestUrl", "https://portal.williambelle.co/ingest/sensor"),
            ("AppId", AppId))));
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://portal.williambelle.co/ingest/sensor")]
    public void An_address_that_cannot_be_posted_to_throws(string ingestUrl)
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddWilliamBelleMonitoring(o =>
        {
            o.IngestUrl = ingestUrl;
            o.AppId = AppId;
            o.SigningKey = Key;
        }));
    }

    /// <summary>
    /// The endpoint answers 400 rather than 401 for an id it cannot parse, so
    /// this would be diagnosed as a broken payload instead of a wrong id.
    /// </summary>
    [Fact]
    public void An_application_id_that_is_not_a_guid_throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddWilliamBelleMonitoring(o =>
        {
            o.IngestUrl = "https://portal.williambelle.co/ingest/sensor";
            o.AppId = "williambelle-co";
            o.SigningKey = Key;
        }));
    }

    [Fact]
    public void Durations_come_from_the_section_when_it_carries_them()
    {
        var services = new ServiceCollection();

        Assert.True(services.TryAddWilliamBelleMonitoring(Section(
            ("IngestUrl", "https://portal.williambelle.co/ingest/sensor"),
            ("AppId", AppId),
            ("SigningKey", Key),
            ("Interval", "01:00:00"),
            ("EnvironmentName", "Staging"))));

        var options = services.BuildServiceProvider()
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<MonitoringOptions>>().Value;

        Assert.Equal(TimeSpan.FromHours(1), options.Interval);
        Assert.Equal("Staging", options.EnvironmentName);
    }

    [Fact]
    public void A_duration_that_is_not_one_throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.TryAddWilliamBelleMonitoring(Section(
            ("IngestUrl", "https://portal.williambelle.co/ingest/sensor"),
            ("AppId", AppId),
            ("SigningKey", Key),
            ("Interval", "every day"))));
    }
}
