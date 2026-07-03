# RazorSearch

Search rendered Umbraco pages with Umbraco Search.

## Installation

Requires **Umbraco 17**.

```bash
dotnet add package Umbraco.Community.RazorSearch
```

## Host setup

RazorSearch references `Umbraco.Cms.Search.Core`, but it does **not** bootstrap Umbraco Search for the consuming application.

Your application is responsible for:

- calling `AddSearchCore()`
- registering the provider you want to use
- configuring the published content index used by that provider

`AddSearchCore()` comes from the `Umbraco.Cms.Search` package.

Example:

```csharp
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddSearchCore();
```

Most projects do not need extra `RazorSearch` configuration unless they want to change extraction behavior or exclude content by content type or property alias.

If your provider uses an explicit field schema, register the RazorSearch aliases in that provider as well. For Examine, add `RazorSearch_Title`, `RazorSearch_Summary`, `RazorSearch_Heading`, and `RazorSearch_Content` under `Umbraco:CMS:Search:Examine:Fields`.

## What the package adds

- EF Core-backed snapshot persistence
- automatic database migrations for the snapshot table
- a background render queue
- an HTTP renderer
- configurable extraction for title, summary, headings, and body text from rendered HTML and published Umbraco properties
- a custom `IContentIndexer` that contributes snapshot fields to Umbraco Search
- management endpoints and backoffice tooling for rebuild and backfill flows

## Important notes

- Queueing or rebuilding snapshots now refreshes the published content index through `Umbraco.Cms.Search.Core` after snapshot writes and deletes, but provider-managed field schemas still need the RazorSearch aliases registered explicitly.
- If you use `SearchRenderingContext.IsActive` in views, RazorSearch will automatically use a fallback render token during built-in HTTP rendering. Configure `RazorSearch:RenderRequestToken` only if you want full control over that token value.
- The options `ExcludedContentTypeAliases` and `ExcludeFromSearchPropertyAlias` are enforced automatically during queueing, indexing, and runtime search.
- Snapshot extraction can be customized through `RazorSearch:SnapshotExtraction`, including object-based CSS-selector and Umbraco-property sources plus CSS-selector-based removal before text extraction.
