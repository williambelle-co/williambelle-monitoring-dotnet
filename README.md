# WilliamBelle.Monitoring

[![CI](https://github.com/williambelle-co/williambelle-monitoring-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/williambelle-co/williambelle-monitoring-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/WilliamBelle.Monitoring.svg)](https://www.nuget.org/packages/WilliamBelle.Monitoring)

An in-app monitoring agent for .NET applications. It reports, on a schedule,
what only the inside of a running process can see: the runtime it is actually
on, the environment it thinks it is in, and the package versions actually
deployed.

Supports .NET 8, 9 and 10 — every runtime Microsoft currently supports.

That last one matters most. A repository can say a vulnerable package was
upgraded while production still runs the old version — this is what notices.

Node applications are covered by
[`@williambelle-co/monitoring`](https://www.npmjs.com/package/@williambelle-co/monitoring),
which reports the same shape to the same endpoint — so a mixed estate lands in
one place.

## Usage

```csharp
builder.Services.TryAddWilliamBelleMonitoring(builder.Configuration.GetSection("Monitoring"));
```

Reads `IngestUrl`, `AppId` and `SigningKey` from that section, and optionally
`Interval`, `Timeout` and `EnvironmentName`. Keep the signing key in a secret
store, never in source.

**A section with none of those values registers nothing and returns `false`.** An
application is usually monitored in production and not on a developer machine,
so that is the normal case rather than an error. A section holding *some* of
them is a half-finished deployment — it throws, rather than starting an
application that looks monitored and reports nothing.

Where the values do not come from configuration:

```csharp
builder.Services.AddWilliamBelleMonitoring(o =>
{
    o.IngestUrl  = ingestUrl;
    o.AppId      = appId;
    o.SigningKey = signingKey;
});
```

The application id and signing key are issued by William Belle LLC.

## What it does, exactly

Every 24 hours by default it collects:

- the runtime servicing level (`RuntimeInformation.FrameworkDescription`)
- the environment name — which catches `Development` running in production
- the third-party packages this deployment shipped with, and their versions

It signs that with HMAC-SHA256 and POSTs it. That is the complete data surface.

The package list is read from the dependency manifest the runtime writes beside
the application, so it is the set that was restored — complete before the
process has served anything, and identical on two hosts running the same build.
Deployments that ship without such a manifest, single-file and
ahead-of-time-compiled ones, fall back to reporting loaded assemblies.

## What it deliberately does not do

- **It accepts no inbound anything.** No endpoint, no commands, no remote
  configuration, no code execution. The channel is one-way by construction.
- **It collects no logs, no request payloads, and no user data.**
- **It cannot break the host** once it is running. Every reporting cycle is
  wrapped in catch-log-continue; an unreachable ingest endpoint costs the host
  nothing. Configuration is the one thing that throws, and it throws at
  registration — a misconfiguration is a bug worth failing on while it is still
  cheap to fix, not a warning nobody reads a day later.
- **It carries no proprietary dependencies.** It ships into other people's
  applications, so it is kept to the smallest possible supply-chain surface:
  Microsoft.Extensions packages a host already has, and nothing else.

Keeping those properties true is the point of this package.

## Building it yourself

```bash
dotnet test          # the signature scheme and what a snapshot contains
dotnet pack src/WilliamBelle.Monitoring -c Release
```

That suite pins this package's signing implementation to the endpoint that
verifies it, and no release goes out without it passing.

The published package is built by GitHub Actions from a tag, using
[Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) —
no long-lived API key exists for this package. Source Link is enabled, so a
debugger can step straight from the published package into this source.

## Versioning

Pre-1.0 while the reporting contract settles. Breaking changes before 1.0 will
be released as a minor version bump and described in the release notes.

## Support

Issued application ids, signing keys, and questions about a monitored
application: [support@williambelle.co](mailto:support@williambelle.co).

Licensed MIT — read it, audit it, and verify it does what this page says.
