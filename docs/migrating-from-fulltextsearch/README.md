# Migrating from FullTextSearch

RazorSearch replaces the rendered-page search use case of `Our.Umbraco.FullTextSearch` for applications using Umbraco Search. It has a different public API and storage format.

| FullTextSearch | RazorSearch |
| --- | --- |
| `ISearchService` | `IRazorSearchService` |
| `Search` request | `Umbraco.Community.RazorSearch.Models.RazorSearch` |
| Synchronous search | `await SearchAsync(request)` |
| Root node integer IDs | Document GUID keys with `UnderRoot` / `UnderRoots` |
| XPath extraction | CSS selectors and published property sources |
| Rendering helper | `SearchRenderingContext.IsActive` |

1. Remove FullTextSearch and its configuration.
2. Install the RazorSearch package line matching your CMS major.
3. Configure `AddSearchCore()` and a provider following [installation](../installation/README.md).
4. Replace calling code with the complete async [usage example](../usage/README.md).
5. Move extraction settings to `Umbraco:Community:RazorSearch:SnapshotExtraction`.
6. Rebuild snapshots for existing published content and verify each language.

A specified culture includes that exact configured culture and invariant documents. An omitted culture searches only invariant documents. There is no request-culture fallback, and neutral culture names do not expand to a language family.

`TitleSources` and `SummarySources` use the first non-empty source. `HeadingSources` and `BodySources` combine sources. Explicit source arrays replace defaults; an empty array disables that source group. Translate XPath exclusions into `RemoveSelectors` CSS selectors.

Result items expose content GUID, published content, URL, title and highlighted summary HTML. They do not expose raw Examine fields or provider scores. RazorSearch has no dedicated fuzzy-matching or wildcard API. Provider-specific query syntax is outside its portable contract. Only public website content, invariant documents and language variants are supported.

The built-in renderer uses HTTP. Custom implementations of `IRazorSearchRenderer` can replace it. Snapshot identity is document key plus culture; switching renderer does not create a parallel snapshot.

Pending rendering work is held in memory and disappears on restart. Stored snapshots remain. Run a manual rebuild after a restart that interrupted work, or after changing templates, shared content or extraction configuration. Rebuilding a provider index alone does not refresh the rendered HTML.
