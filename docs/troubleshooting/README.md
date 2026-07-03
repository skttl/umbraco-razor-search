# Troubleshooting

## Search returns no results

Check these first:

- the host actually calls `AddSearchCore()`
- a search provider is registered
- the published content index is configured
- the provider schema includes the RazorSearch field aliases if the provider requires explicit field registration
- snapshots exist for the content you expect to find
- the relevant render jobs have finished

Snapshot writes now trigger a published-content index refresh automatically, so missing results are usually caused by missing snapshots, incomplete provider setup, or filtering.

## RazorSearch fields do not appear in `Umb_PublishedContent`

That usually means the provider schema does not know about the RazorSearch aliases yet.

The `IContentIndexer` can return values correctly and the refresh can still run, but providers with explicit schemas will not materialize those fields until they are registered.

For the Examine provider, add entries like these under `Umbraco:CMS:Search:Examine:Fields`:

```json
[
  { "PropertyName": "RazorSearch_Title", "FieldValues": "TextsR1" },
  { "PropertyName": "RazorSearch_Summary", "FieldValues": "TextsR2" },
  { "PropertyName": "RazorSearch_Heading", "FieldValues": "TextsR2" },
  { "PropertyName": "RazorSearch_Content", "FieldValues": "Texts" }
]
```

## Search results are stale after publishing

RazorSearch queues snapshot rendering in the background, so there can still be a delay between publish and refreshed search results.

If you need deterministic freshness for validation or QA:

1. publish the content
2. wait for the RazorSearch render job to finish
3. verify the stored snapshot status

## `SearchRenderingContext.IsActive` is never `true`

This usually means the request is not carrying the expected render header and token combination.

The built-in HTTP renderer now sends the render-context header automatically. If you override `RazorSearch:RenderRequestToken`, make sure the middleware and renderer are using the same value.

## Render jobs fail with HTTP errors

Check:

- `RazorSearch:HttpRenderer:BaseAddress`
- `WebRouting:UmbracoApplicationUrl`
- custom headers
- cookies
- timeout
- redirects
- whether the target URL is reachable from the application host

Also inspect the saved failure state through the document status endpoint or RazorSearch logs.

## Documents are skipped during rebuild

That usually means the content had no routable URL.

Typical causes:

- unpublished content
- domains are not configured
- the URL resolves to `#`
- the renderer needs either `HttpRenderer.BaseAddress` or `WebRouting:UmbracoApplicationUrl` to turn relative routes into absolute URLs

## Rebuild says a job is duplicate

That is expected when `RenderQueue.DeduplicateActiveJobs` is enabled.

If the same content and culture are already queued or running, RazorSearch returns the existing job instead of creating another one.

## A moved subtree still seems searchable under old content

Move operations delete subtree snapshots and queue forced rerendering. If search still looks wrong after that, the usual cause is that the new jobs have not finished yet.

If in doubt:

1. queue a subtree rebuild
2. wait for the jobs to finish
3. verify the snapshot status for affected content

## Config-based exclusions do not seem to work

`ExcludedContentTypeAliases` and `ExcludeFromSearchPropertyAlias` are enforced automatically during queueing, indexing, and runtime search.

If excluded content still appears, check:

- the content type alias matches exactly
- the configured opt-out property alias exists on the content type
- the opt-out property has a truthy published value for the relevant culture

## Property-based snapshot sources are empty

Property sources only read **published** values for the content item being rendered.

Check:

- the configured property alias exists
- the property has a published value in the relevant culture
- the value is actually text-like once converted to plain text

If the property value depends on unpublished edits, RazorSearch will not index those edits until they are published.
