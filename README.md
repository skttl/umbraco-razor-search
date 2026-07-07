# RazorSearch

Search rendered Umbraco pages with Umbraco Search.

RazorSearch stores rendered HTML snapshots, extracts searchable text from them, and writes that data into an internal dedicated Umbraco Search index owned by the package.

## Requirements

RazorSearch requires **Umbraco 17**.

The package is intentionally provider-agnostic:

- it does not call `AddSearchCore()` for you
- it does not register a search provider for you
- it does not take over your published content index configuration

Your host application must bootstrap Umbraco Search first.

`AddSearchCore()` comes from the `Umbraco.Cms.Search` package.

## Quick Start

Install the package:

```bash
dotnet add package Umbraco.Community.RazorSearch
```

If your host uses `Umbraco.Cms.Search.Provider.Examine`, also install the Examine companion package:

```bash
dotnet add package Umbraco.Community.RazorSearch.Examine
```

Enable Umbraco Search in your host application:

```csharp
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddSearchCore();
```

Then make sure your host application also:

1. registers the Umbraco Search provider you want to use
2. configures that provider for the host application
3. backfills existing content after first install so snapshots exist for older pages

For Examine hosts, the companion package owns the physical Lucene index and field definitions for RazorSearch's internal index alias. Consumers still do not need to know or configure that alias directly.

Most projects do not need extra `RazorSearch` configuration unless they want to change extraction behavior or exclude content by content type or property alias.

## Documentation

- [Documentation Index](docs/README.md)
- [Installation](docs/installation/README.md)
- [Configuration](docs/configuration/README.md)
- [Usage](docs/usage/README.md)
- [Indexing and Reindexing](docs/indexing/README.md)
- [Troubleshooting](docs/troubleshooting/README.md)
- [Customization](docs/customization/README.md)
- [Migrating from FullTextSearch](docs/migrating-from-fulltextsearch/README.md)

## What RazorSearch Adds

- EF Core-backed snapshot persistence via Umbraco's EF Core abstraction
- automatic database migrations for the RazorSearch snapshot table on application start
- a background render queue
- configurable extraction for title, summary, headings, and body content from rendered HTML and published Umbraco properties
- an internal dedicated RazorSearch search index backed by stored snapshots
- management endpoints and backoffice tooling for queueing rebuilds

## Good To Know

- Snapshot writes and deletes now trigger a RazorSearch index refresh through `Umbraco.Cms.Search.Core`.
- If you rely on `SearchRenderingContext.IsActive` in your views, RazorSearch will automatically use a fallback render token during built-in HTTP rendering. Configure `RazorSearch:RenderRequestToken` only if you want full control over that token value.
- `ExcludedContentTypeAliases` and `ExcludeFromSearchPropertyAlias` are enforced automatically during queueing and through index-native filters at search time.
- Snapshot extraction is configurable through `RazorSearch:SnapshotExtraction`, including object-based CSS-selector and Umbraco-property sources plus CSS-selector-based removal.
