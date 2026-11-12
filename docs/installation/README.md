# Installation

Use the package line matching the CMS major. `main` targets Umbraco 19.0.0-rc1 and .NET 11. `v18/main` maintains Umbraco 18 and `v17/main` maintains Umbraco 17, both on .NET 10. SQL Server supports single-server and the documented load-balanced topology. SQLite is intended for one app instance but is not release-verified; see the [known limitations](../release-notes.md).

## Examine application

```powershell
dotnet add package Umbraco.Community.RazorSearch.Examine --version 19.0.0-beta01
```

The companion installs core transitively. For Umbraco 18 use `18.0.0`; for Umbraco 17 use `17.0.0`.

With Umbraco `19.0.0-rc1` and .NET SDK `11.0.100`, static-asset compression fails with `ApplyCompressionNegotiation` for `app-auth-callback.element.js`, including in a stock CMS app without RazorSearch. Until the CMS fixes its asset endpoints, add this temporary workaround to the consuming application's `.csproj`, as in the demo:

```xml
<PropertyGroup>
  <CompressionEnabled>false</CompressionEnabled>
</PropertyGroup>
```

A complete `Program.cs`:

```csharp

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
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

Umbraco 19 registers Search and the default Examine provider automatically. The companion registers the physical RazorSearch Examine index and fields. Your app owns any ordinary published-content index configuration.

For another provider, install core directly and follow that provider's Umbraco 19 setup. Core does not register a provider automatically.

## Build and start

Build once after installing. Umbraco copies `appsettings-schema.Umbraco.Community.RazorSearch.json` into the app and adds its reference to `appsettings-schema.json`. Use `"$schema": "appsettings-schema.json"` in appsettings files for editor completion and validation.

Start the app to create the snapshot table. Sign into the backoffice as an administrator and start a global rebuild in RazorSearch. Rebuild acceptance returns promptly; document enumeration and rendering continue in the background. Wait for completion before checking the [search example](../usage/README.md).

## Updating an unreleased development database

The first stable release replaces the experimental snapshot schema. No upgrade from the unreleased schema is supported. For a disposable development site, create a fresh database and rebuild snapshots. Do not point the package at an existing development database containing the old RazorSearch table without explicitly resetting that package's data first. Published Umbraco content is the source of truth for rebuilding snapshots.

## Deployment

For load balancing, deploy matching package/configuration versions on every node. Start the dedicated backoffice first so migrations complete before frontend nodes serve queries. Configure its internal rendering origin as described in [indexing](../indexing/README.md).

After deploying changed templates, shared template content or extraction settings, manually rebuild the affected documents or the full site. An ordinary Umbraco Search index rebuild reads stored snapshots; it does not render pages again.
