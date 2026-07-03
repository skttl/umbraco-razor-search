# Usage

RazorSearch is consumed through `IRazorSearchService` and the `RazorSearch` request model.

## Inject the service

```csharp
using Umbraco.Community.RazorSearch;

public sealed class SearchController : RenderController
{
    private readonly IRazorSearchService _razorSearchService;

    public SearchController(
        IRazorSearchService razorSearchService,
        ILogger<RenderController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
        _razorSearchService = razorSearchService;
    }
}
```

## Search from a controller

```csharp
using Umbraco.Community.RazorSearch;

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
        .ExcludeContentTypes("folder", "searchPage")
        .Page(pageNumber, 10);

    searchPage!.SearchResult = await _razorSearchService.SearchAsync(search);
    return CurrentTemplate(searchPage);
}
```

## Search from any other service or controller

The same API works outside `RenderController`:

```csharp
using Umbraco.Community.RazorSearch;
using Umbraco.Community.RazorSearch.Models;

public sealed class SearchFacade(IRazorSearchService razorSearchService)
{
    public Task<IRazorSearchResult> SearchSiteAsync(string query, Guid rootKey, string culture, CancellationToken cancellationToken)
    {
        var search = new RazorSearch(query)
            .UnderRoot(rootKey)
            .InCulture(culture)
            .Page(1, 20);

        return razorSearchService.SearchAsync(search, cancellationToken);
    }
}
```

## Render results in a view

```cshtml
@using Umbraco.Community.RazorSearch.Models
@if (Model.SearchResult is IRazorSearchResult result && result.Items.Count > 0)
{
    <p>@result.Total result(s)</p>

    <ul>
    @foreach (var item in result.Items)
    {
        <li>
            <a href="@item.Url">@item.Title</a>
            <div>@Html.Raw(item.SummaryHtml)</div>
        </li>
    }
    </ul>
}
else
{
    <p>No results found.</p>
}
```

## Use render-only markup in views

If you want markup to appear only during RazorSearch rendering, use `SearchRenderingContext.IsActive`:

```cshtml
@using Umbraco.Community.RazorSearch

@if (SearchRenderingContext.IsActive)
{
    <div>This text is only included in RazorSearch rendering.</div>
}
```

Important:

- this only becomes active when the incoming request carries the expected render token
- with the built-in HTTP renderer, RazorSearch sends that token automatically
- configure `RazorSearch:RenderRequestToken` only if you want to override the default fallback token

## Use `SearchRenderingContext.IsActive` to remove or suppress markup

This pattern is useful when you want RazorSearch rendering to omit markup that should not affect the extracted search text.

Example view:

```cshtml
@using Umbraco.Community.RazorSearch

<article>
    <h1>@Model.Value("pageTitle")</h1>

    @if (SearchRenderingContext.IsActive is false)
    {
        <aside class="promo-banner">
            Sign up for our newsletter
        </aside>
    }
</article>
```

In this case, the promo markup is shown on the site, but not when RazorSearch renders the page for snapshot extraction.

You can also combine `SearchRenderingContext.IsActive` with `RemoveSelectors` when you need search-only helper markup during rendering and want to strip it before extraction.

Example:

```json
{
  "RazorSearch": {
    "SnapshotExtraction": {
      "BodySources": [
        { "Type": "selector", "Selector": "body" }
      ],
      "RemoveSelectors": [".razor-search-only"]
    }
  }
}
```

How it works:

- `SearchRenderingContext.IsActive` lets your view render less markup during RazorSearch snapshot generation
- this is often the simplest way to exclude banners, navigation fragments, or other non-search content
- `RemoveSelectors` is still useful when markup must exist during rendering, but should be stripped before text extraction

Use this when you want search rendering to be cleaner than the public page output.

## Request model capabilities

The current `RazorSearch` request model supports:

- `InCulture(...)`
- `UnderRoot(...)`
- `UnderRoots(...)`
- `IncludeContentType(...)`
- `IncludeContentTypes(...)`
- `ExcludeContentType(...)`
- `ExcludeContentTypes(...)`
- `Page(pageNumber, pageSize)`
- `SkipTake(skip, take)`

## Validation rules

`SearchAsync(...)` validates the request and will throw if:

- the query text is empty
- `Take` is less than `1`
- `Skip` is negative
- any root key is an empty `Guid`
