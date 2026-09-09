# Installation

This branch targets Umbraco 17.6.2+ and Search Core 17.1.0+ within the 17.x line. It uses .NET 10. SQL Server supports single-server and the documented load-balanced topology. SQLite is intended for one app instance but is not release-verified in this beta; see the [known limitations](../release-notes.md).

## Examine application

```powershell
dotnet add package Umbraco.Community.RazorSearch.Examine --version 17.0.0-beta.1
```

The companion installs core transitively. For Umbraco 17 use `17.0.0-beta.1`. During local acceptance, install from the `artifacts/packages` feed produced by `build/Validate.ps1`; these versions are not available on NuGet until published.

A complete `Program.cs`:

```csharp
using Umbraco.Cms.Search.Core.DependencyInjection;
using Umbraco.Cms.Search.Provider.Examine.DependencyInjection;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddSearchCore()
    .AddExamineSearchProvider()
    .Build();

WebApplication app = builder.Build();
await app.BootUmbracoAsync();
app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });
await app.RunAsync();
```

`AddSearchCore()` is provided by `Umbraco.Cms.Search.Core`. The companion registers the physical RazorSearch Examine index and fields. Your app owns provider registration and any ordinary published-content index configuration.

For another provider, install core directly and replace `AddExamineSearchProvider()` with your provider's setup. Core does not register a provider automatically.

## Build and start

Build once after installing. Umbraco copies `appsettings-schema.Umbraco.Community.RazorSearch.json` into the app and adds its reference to `appsettings-schema.json`. Use `"$schema": "appsettings-schema.json"` in appsettings files for editor completion and validation.

Start the app to create the snapshot table. Sign into the backoffice as an administrator and start a global rebuild in RazorSearch. Rebuild acceptance returns promptly; document enumeration and rendering continue in the background. Wait for completion before checking the [search example](../usage/README.md).

## Updating an unreleased development database

This first beta replaces the experimental snapshot schema. No upgrade from the unreleased schema is supported. For a disposable development site, create a fresh database and rebuild snapshots. Do not point the beta at an existing development database containing the old RazorSearch table without explicitly resetting that package's data first. Published Umbraco content is the source of truth for rebuilding snapshots.

## Deployment

For load balancing, deploy matching package/configuration versions on every node. Start the dedicated backoffice first so migrations complete before frontend nodes serve queries. Configure its internal rendering origin as described in [indexing](../indexing/README.md).

After deploying changed templates, shared template content or extraction settings, manually rebuild the affected documents or the full site. An ordinary Umbraco Search index rebuild reads stored snapshots; it does not render pages again.
