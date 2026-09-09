# SQLite concurrency diagnostic

This optional executable reproduces concurrent rebuild and partial-culture unpublish using installed NuGet packages, real Umbraco services, EF persistence and HTTP rendering. It is intentionally separate from the regression-test solution: current SQLite failures make it a diagnostic, not a passing release check.

It uses ASP.NET Core TestServer, opens **no TCP listener**, creates a new Temp directory/database for each run and generates temporary installation credentials in memory. It leaves diagnostic files for inspection. A 180-second deadline exits with code 3 if CMS retries prevent completion. It never starts, stops or changes an existing site.

Create a temporary `NuGet.Config` pointing at the candidate package directory and an isolated package cache. For example, replace the two local paths in:

```xml
<configuration>
  <packageSources>
    <clear />
    <add key="candidate" value="C:/Temp/razorsearch-candidate/packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <config>
    <add key="globalPackagesFolder" value="C:/Temp/razorsearch-diagnostic-cache" />
  </config>
</configuration>
```

Restore with that config and run each mode separately:

```powershell
dotnet restore tests/diagnostics/sqlite/SqliteDiagnostic.csproj --configfile C:/Temp/NuGet.Config
dotnet run --project tests/diagnostics/sqlite/SqliteDiagnostic.csproj --no-restore -- Shared
dotnet run --project tests/diagnostics/sqlite/SqliteDiagnostic.csproj --no-restore -- Private
```

The defaults are CMS 17.6.2 / RazorSearch 17.0.0-beta.1. Set the `UmbracoVersion` and `RazorSearchVersion` MSBuild properties together when checking another candidate. The package is responsible for neither registering Search nor selecting a provider; this consumer explicitly configures both.

The driver waits for `IContentRoutingReadiness.IsReady`, which includes completion of startup notification handlers during unattended installation/upgrade. It then publishes an invariant document and an English/Danish document, assigns culture domains and waits for both variant snapshots. Ten iterations request a rebuild followed immediately by Danish unpublish. Each checks English retention, Danish removal and no immediate resurrection before republishing Danish. The final assertion requires a drained render queue with zero failed jobs.

The diagnostic sets SQLite's default command timeout to five seconds to expose persistence failures quickly; CMS NPoco retry policy can still exceed that timeout. The 180-second deadline is a test bound, not the production retry duration. TestServer replaces network transport only. This does not verify real process restart, browser behavior, load balancing or the full rebuild above 5,000 documents.

See [release validation](../../../.agents/docs/release-validation.md) for observed failures and their limits.
