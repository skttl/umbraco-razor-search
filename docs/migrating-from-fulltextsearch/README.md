# Migrating from FullTextSearch

This guide is verified against the current RazorSearch package code, not against earlier planning notes.

## What still maps cleanly

These migrations are still valid:

| FullTextSearch | RazorSearch |
| --- | --- |
| `Search` | `RazorSearch` |
| `ISearchService` | `IRazorSearchService` |
| `IFullTextSearchResult` | `IRazorSearchResult` |
| `Search(search, currentPage)` | `await SearchAsync(search)` |
| `SetCulture(...)` | `InCulture(...)` |
| `AddRootNodeId(...)` | `UnderRoot(...)` |
| `AddRootNodeIds(...)` | `UnderRoots(...)` |
| `AddAllowedContentTypes(...)` | `IncludeContentTypes(...)` |
| `FullTextSearchHelper.IsRenderingActive()` | `SearchRenderingContext.IsActive` |

## Typical service migration

Before:

```csharp
using Our.Umbraco.FullTextSearch.Interfaces;

private readonly ISearchService _searchService;

public SearchController(ISearchService searchService, ...)
{
    _searchService = searchService;
}
```

After:

```csharp
using Umbraco.Community.RazorSearch;

private readonly IRazorSearchService _razorSearchService;

public SearchController(IRazorSearchService razorSearchService, ...)
{
    _razorSearchService = razorSearchService;
}
```

## Typical controller migration

```csharp
public override async Task<IActionResult> Index()
{
    var searchPage = CurrentPage as ContentModels.Search;
    string query = Request.Query["q"].ToString();

    if (string.IsNullOrWhiteSpace(query))
    {
        searchPage!.SearchResult = null;
        return CurrentTemplate(searchPage);
    }

    int.TryParse(Request.Query["p"].ToString(), out int pageNumber);
    pageNumber = pageNumber < 1 ? 1 : pageNumber;

    var search = new RazorSearch(query)
        .InCulture(CurrentPage.GetCultureFromDomains().ToLowerInvariant())
        .UnderRoot(CurrentPage.Root().Key)
        .Page(pageNumber, 10);

    searchPage!.SearchResult = await _razorSearchService.SearchAsync(search);
    return CurrentTemplate(searchPage);
}
```

## Snapshot extraction model

RazorSearch now has a package-level source configuration model under `RazorSearch:SnapshotExtraction`.

Instead of FullTextSearch-style XPath configuration, RazorSearch uses ordered source definition objects for:

- `TitleSources`
- `SummarySources`
- `HeadingSources`
- `BodySources`

Supported source types are:

- `selector`
- `property`

Example:

```json
{
  "RazorSearch": {
    "SnapshotExtraction": {
      "TitleSources": [
        { "Type": "property", "Alias": "seoTitle" },
        { "Type": "selector", "Selector": "title" }
      ],
      "SummarySources": [
        { "Type": "property", "Alias": "seoDescription" },
        { "Type": "selector", "Selector": "meta[name='description']", "Attribute": "content" }
      ],
      "BodySources": [
        { "Type": "property", "Alias": "mainContent" },
        { "Type": "selector", "Selector": "body" }
      ],
      "RemoveSelectors": [".skip-search"]
    }
  }
}
```

Important behavior:

- `TitleSources` and `SummarySources` stop at the first non-empty value.
- `HeadingSources` and `BodySources` merge multiple non-empty values.
- property sources read published Umbraco property values for the current content and culture.

## What does not exist in RazorSearch

These are the most important corrections to older migration notes.

### No XPath model

If your FullTextSearch solution relied on XPath-based configuration such as `XPathsToRemove`, you will need to translate that to CSS selectors under `RemoveSelectors` and selector-based source definitions.

### No built-in non-HTTP renderer

FullTextSearch setups sometimes relied on different rendering strategies. RazorSearch currently ships with one built-in renderer: HTTP.

### No score or raw index field access in result items

If your existing code depends on raw Examine fields or score values, that code will need a redesign. RazorSearch result items expose title, URL, published content, and highlighted summary HTML, but not score.

### No implicit `VariationContext` fallback behavior

RazorSearch uses culture only when you set it explicitly on the `RazorSearch` request. If you need culture-specific behavior, keep setting culture explicitly during migration.

## Indexing model difference to plan for

This is the operational difference most likely to surprise existing FullTextSearch users:

- RazorSearch stores rendered snapshots in its own table
- the published content search index picks up snapshot fields through `IContentIndexer`
- RazorSearch now refreshes the published-content index automatically after snapshot writes and deletes

That means migration should still include:

1. snapshot backfill or rebuild
2. waiting for the resulting jobs to finish before validating search results

## Recommended migration checklist

1. Remove `Our.Umbraco.FullTextSearch`.
2. Install `Umbraco.Community.RazorSearch`.
3. Enable `AddSearchCore()` in the host.
4. Register your actual search provider in the host.
5. Replace `ISearchService` with `IRazorSearchService`.
6. Replace `Search` with `RazorSearch`.
7. Make calling code async.
8. Move paging to `Page(...)` or `SkipTake(...)`.
9. Move root scoping to `UnderRoot(...)` or `UnderRoots(...)`.
10. Move content-type filtering to request-level include or exclude methods where needed.
11. Replace `FullTextSearchHelper.IsRenderingActive()` with `SearchRenderingContext.IsActive`.
12. Configure `RenderRequestToken` only if you need to override RazorSearch's fallback token for render-only markup.
13. Translate older extraction rules to `SnapshotExtraction` source objects and `RemoveSelectors`.
14. Queue a snapshot backfill.

## Safe expectation setting

The easiest way to think about the migration is:

- the core idea is the same
- the request and service API are simpler
- package-level extraction is now configurable with objects instead of XPath
- some older FullTextSearch escape hatches do not exist
