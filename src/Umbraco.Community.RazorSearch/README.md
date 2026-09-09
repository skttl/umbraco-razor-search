# RazorSearch

Search rendered Umbraco pages with Umbraco Search. RazorSearch stores HTML snapshots, extracts title, headings and body text, and contributes them to a dedicated search index.

This branch targets Umbraco 17.6.2+ within 17.x, .NET 10 and Umbraco Search Core 17.1.0+. The first releases are beta packages. SQLite is not release-verified in this beta; known concurrency limitations are recorded in the release notes.

## Install with Examine

```powershell
dotnet add package Umbraco.Community.RazorSearch.Examine --version 17.0.0-beta.1
```

The companion includes the core package and creates its physical Examine index. Enable Search and the provider in the consuming application's `Program.cs`:

```csharp
using Umbraco.Cms.Search.Core.DependencyInjection;
using Umbraco.Cms.Search.Provider.Examine.DependencyInjection;

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddSearchCore()
    .AddExamineSearchProvider()
    .Build();
```

Keep the application's normal Umbraco boot, middleware and endpoint setup. Start the site, then queue a rebuild of existing published content from the RazorSearch backoffice dashboard.

Core is provider-agnostic. It does not call `AddSearchCore()`, register a provider or configure the application's published-content index. For a different provider, install `Umbraco.Community.RazorSearch` directly and configure that provider yourself. Examine is the provider covered by the beta acceptance matrix.

## Configuration

```json
{
  "Umbraco": {
    "Community": {
      "RazorSearch": {
        "SnapshotExtraction": {
          "BodySources": [{ "Type": "selector", "Selector": "main" }]
        }
      }
    }
  }
}
```

Settings live under `Umbraco:Community:RazorSearch`. The NuGet package includes an appsettings-schema. The first build after installation copies it to the app and registers it in `appsettings-schema.json`, including when core is installed transitively through the companion.

## Search

```csharp
using Umbraco.Community.RazorSearch;
using Umbraco.Community.RazorSearch.Models;

// Inject IRazorSearchService as searchService.
var request = new RazorSearch("umbraco")
    .InCulture("da-DK")
    .Page(1, 10);
IRazorSearchResult result = await searchService.SearchAsync(request);
```

A specified culture must match a configured Umbraco culture. The search includes that culture and invariant documents. Without a culture it searches invariant documents only. There is no implicit request-culture fallback.

## Operation

Rendering and indexing happen asynchronously after publication. Each document and culture has one current snapshot. A temporary rendering failure preserves the last usable snapshot. Unpublishing, deleting and excluding content remove it from search.

The render queue and its history live in memory. Restarting the app loses pending work. Run a manual rebuild when necessary, including after changes to templates, shared content or extraction rules.

Load balancing targets one dedicated backoffice server and multiple frontends with a shared SQL Server database and separate local Examine indexes. The backoffice owns rendering; Umbraco Search distributes index refreshes. Configure an internal render destination to avoid a stale frontend or CDN response. Multiple active backoffice servers are outside the beta scope.

## Documentation

- [Installation](https://github.com/skttl/umbraco-razor-search/blob/main/docs/installation/README.md)
- [Configuration](https://github.com/skttl/umbraco-razor-search/blob/main/docs/configuration/README.md)
- [Usage](https://github.com/skttl/umbraco-razor-search/blob/main/docs/usage/README.md)
- [Indexing and load balancing](https://github.com/skttl/umbraco-razor-search/blob/main/docs/indexing/README.md)
- [Troubleshooting](https://github.com/skttl/umbraco-razor-search/blob/main/docs/troubleshooting/README.md)
- [Customization](https://github.com/skttl/umbraco-razor-search/blob/main/docs/customization/README.md)
- [Migrating from FullTextSearch](https://github.com/skttl/umbraco-razor-search/blob/main/docs/migrating-from-fulltextsearch/README.md)
- [Beta release notes](https://github.com/skttl/umbraco-razor-search/blob/main/docs/release-notes.md)
