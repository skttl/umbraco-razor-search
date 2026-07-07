# Installation

This guide covers the package installation itself and the host-level setup RazorSearch expects.

## Requirements

- Umbraco 17
- A host application that uses Umbraco Search
- A configured Umbraco Search provider for the host application

RazorSearch depends on `Umbraco.Cms.Search.Core`, but it does not bootstrap that pipeline for you.

## Step 1: Install the package

```bash
dotnet add package Umbraco.Community.RazorSearch
```

If the host uses `Umbraco.Cms.Search.Provider.Examine`, also install:

```bash
dotnet add package Umbraco.Community.RazorSearch.Examine
```

## Step 2: Enable Umbraco Search in the host

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

For Examine specifically, the companion package registers the physical Lucene index and field definitions for RazorSearch's internal index alias.

## Step 3: Register your provider

RazorSearch owns its own internal index alias, but your host application still owns the provider setup.

Make sure your host application also:

- registers the search provider you want to use
- configures that provider for the host application

## Step 4: Start the site

When the site starts, RazorSearch automatically registers its services and applies any pending migrations for its snapshot table.

## Step 5: Backfill existing content

RazorSearch only creates snapshots when content is rendered through its queue.

For a new installation, queue a rebuild or backfill after startup so existing published content becomes searchable.

## What the package registers automatically

Once installed, RazorSearch adds:

- a snapshot `DbContext`
- EF Core-backed snapshot storage
- notification handlers for publish, unpublish, delete, move, and recycle-bin events
- a background render queue and hosted service
- the HTTP renderer
- an internal dedicated RazorSearch index registration and indexing pipeline
- management APIs and backoffice actions

The package also applies its own pending EF Core migrations on application start.

## First-time setup checklist

After installation, the usual flow is:

1. Install the NuGet package.
2. Enable `AddSearchCore()` in the host.
3. Register and configure your search provider for the host application.
4. Add RazorSearch configuration if needed.
5. Start the site and let RazorSearch create or migrate its snapshot table.
6. Queue a snapshot backfill for existing content.
7. Wait for the queued render jobs to finish so snapshot writes can refresh the internal RazorSearch index.

## What is not done automatically

RazorSearch does not:

- register your provider
- take over your published content index
- backfill existing published content automatically on first install

That distinction matters on new installs and after large rebuilds.
