# Indexing and Reindexing

RazorSearch has two related but separate concerns:

1. rendering pages into stored snapshots
2. contributing snapshot text into the published content index through Umbraco Search

That distinction is important when you troubleshoot freshness issues.

## What is stored in a snapshot

For each successful render, RazorSearch stores:

- title text
- meta description text
- heading text from `h1` through `h6`
- body text
- the combined snapshot text
- the final URL and metadata about the render result

## When snapshotting happens automatically

The package listens to content lifecycle notifications.

### Publish and republish

On publish, RazorSearch queues render jobs for each routable published culture.

### Unpublish

On unpublish, RazorSearch deletes snapshots for unpublished cultures, or all snapshots if the content is no longer published at all.

### Delete

On delete, RazorSearch removes snapshots for the deleted content.

### Move

On move, RazorSearch deletes snapshots for the affected subtree and queues a forced rerender of the moved subtree.

### Move to recycle bin

On recycle-bin moves, RazorSearch removes snapshots for the affected subtree.

## When search indexing happens

RazorSearch contributes fields through `IContentIndexer`, not by owning a separate search index.

The current field mapping is:

- title text -> `TextsR1`
- summary text -> `TextsR2`
- heading text -> `TextsR2`
- body text -> `Texts`

Those values are emitted under these fixed field aliases:

- `RazorSearch_Title`
- `RazorSearch_Summary`
- `RazorSearch_Heading`
- `RazorSearch_Content`

If your provider requires explicit field registration, those aliases must also be declared in the provider schema before they will appear in the provider index. For Examine, that means adding matching entries under `Umbraco:CMS:Search:Examine:Fields`.

## Current indexing flow

Snapshot rendering and published-content index refresh are now connected.

In the current package version:

- the background render job stores snapshot data in the RazorSearch table
- the custom `IContentIndexer` supplies that snapshot data to Umbraco Search
- RazorSearch requests a published-content index refresh after snapshot writes and deletes
- the search provider must already know the RazorSearch field aliases if it uses an explicit schema

In practice, that means:

- normal publish, rebuild, and delete flows no longer need a separate manual provider reindex step just to pick up fresh RazorSearch fields
- search freshness can still lag briefly because rendering happens in the background before the index refresh is requested

## How to backfill or rebuild snapshots

### From the backoffice

The package includes:

- a document action for queueing a single document or a document tree
- a management view for backfilling all published content

### Through the management API

Queue one document:

`POST /umbraco/management/api/v1/razor-search/document/{id}/queue`

Alias:

`POST /umbraco/management/api/v1/razor-search/document/{id}/rebuild`

Request body:

```json
{
  "includeDescendants": false,
  "maxDocuments": 250
}
```

Queue all published content:

`POST /umbraco/management/api/v1/razor-search/published/rebuild`

Request body:

```json
{
  "maxDocuments": 250
}
```

Check document status:

`GET /umbraco/management/api/v1/razor-search/document/{id}/status`

These endpoints require backoffice authorization.

## Recommended rebuild flow

For deterministic results after install or recovery:

1. Queue a RazorSearch backfill or rebuild.
2. Wait for render jobs to finish.
3. Verify that the expected content appears in search results.

## What can prevent a document from being queued

A document is skipped if it has no routable URL for the culture being processed.

Typical reasons:

- the content is not published
- the URL resolves to `#`
- the site has no routable absolute URL and no usable `HttpRenderer.BaseAddress`
