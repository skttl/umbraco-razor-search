# Troubleshooting

## Search returns no results

Check these first:

- the host actually calls `AddSearchCore()`
- a search provider is registered
- if the host uses Examine, `Umbraco.Community.RazorSearch.Examine` is installed
- the search provider is able to initialize the internal RazorSearch index
- snapshots exist for the content you expect to find
- the relevant render jobs have finished

Snapshot writes now trigger an internal RazorSearch index refresh automatically, so missing results are usually caused by missing snapshots, incomplete provider setup, or filtering.

## RazorSearch fields do not appear in the internal RazorSearch index

First make sure snapshots exist and the related render jobs have finished.

You can verify that by:

1. queueing a rebuild for the document or subtree if needed
2. checking the document status endpoint: `GET /umbraco/management/api/v1/razor-search/document/{id}/status`
3. confirming the document has a successful snapshot for the culture you expect
4. checking RazorSearch logs if the job failed or never completed

If you need to backfill content first, use the RazorSearch backoffice management view or the rebuild endpoints described in [../indexing/README.md](../indexing/README.md).

RazorSearch writes its fields through its own internal indexing pipeline, so if results are still missing after snapshots have completed, verify the index refresh has completed and then inspect provider-specific behavior.

## Examine says the RazorSearch index could not be found

That usually means the host has `Umbraco.Cms.Search.Provider.Examine`, but not the companion package that creates the physical Lucene index for RazorSearch.

Install `Umbraco.Community.RazorSearch.Examine`, restart the site, and rerun the rebuild.

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
