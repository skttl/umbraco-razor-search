# Installation

This guide covers the package installation itself and the host-level setup RazorSearch expects.

## Requirements

- Umbraco 17
- A host application that uses Umbraco Search
- A configured Umbraco Search provider for published content

RazorSearch depends on `Umbraco.Cms.Search.Core`, but it does not bootstrap that pipeline for you.

## Install the package

```bash
dotnet add package Umbraco.Community.RazorSearch
```

## Register Umbraco Search in the host

At minimum, your host must call `AddSearchCore()`:

`AddSearchCore()` comes from the `Umbraco.Cms.Search` package.

```csharp
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddSearchCore();
```

RazorSearch does not register a concrete search provider. If you use Examine or another provider, that provider registration belongs in the consuming application as well.

## What the package registers automatically

Once installed, RazorSearch adds:

- a snapshot `DbContext`
- EF Core-backed snapshot storage
- notification handlers for publish, unpublish, delete, move, and recycle-bin events
- a background render queue and hosted service
- the HTTP renderer
- a custom `IContentIndexer`
- management APIs and backoffice actions

The package also applies its own pending EF Core migrations on application start.

## First-time setup checklist

After installation, the usual flow is:

1. Install the NuGet package.
2. Enable `AddSearchCore()` in the host.
3. Register and configure your search provider for published content.
4. Add RazorSearch configuration if needed.
5. Start the site and let RazorSearch create or migrate its snapshot table.
6. Queue a snapshot backfill for existing content.
7. Wait for the queued render jobs to finish so snapshot writes can refresh the published content index.

## What is not done automatically

RazorSearch does not:

- register your provider
- configure your published content index
- backfill existing published content automatically on first install

That distinction matters on new installs and after large rebuilds.
