# Troubleshooting

## No results

Confirm `AddSearchCore()` and a provider are registered. Examine apps also need the RazorSearch Examine companion to create the physical index. Rebuild existing published content after installation and wait for rendering and the subsequent Search refresh.

Check culture explicitly. No culture searches invariant documents only. `da-DK` searches that configured culture plus invariant documents. `da` does not expand to `da-DK`. Protected and excluded documents are omitted from public results.

## Stale results

Inspect the document snapshot and latest rendering attempt. Failed rendering preserves the last usable text. The UI's expected index content comes from snapshots; it is not proof of what the provider currently contains.

Allow for Examine's searcher refresh and, on multiple nodes, delivery of the distributed index update. Queue completion can precede visibility in search. Check the actual query/index on each node before diagnosing a missing update. If results remain stale, compare the stored snapshot with the provider's index and inspect the indexing/distributed-cache logs.

A template or shared-content change requires a manual snapshot rebuild. A provider index rebuild alone cannot update stored HTML. A restart loses pending in-memory jobs, so rerun interrupted rebuilds.

Result links are resolved from current published content. Check the document's current route and culture/domain configuration when a link is unavailable; changing the internal rendering address does not change the public result URL.

## Rendering fails

Verify the public URL is routable, HTML is returned with a successful status, and authentication/CDN rules do not redirect the request. Redirects are disabled by default. If enabled, they must stay within the original public origin.

For relative routes, configure `Umbraco:Community:RazorSearch:HttpRenderer:BaseAddress`. On load-balanced sites configure `HttpRenderer:RenderBaseAddress` to the dedicated backoffice listener, with a shared `RenderRequestToken`. The network destination must accept the public Host header. A frontend or CDN may otherwise return stale HTML.

A failing attempt does not remove an existing usable snapshot. HTTP status and error information are visible separately. Unpublication and exclusion still remove documents from search.

For HTTP errors, check the public URL, `HttpRenderer:BaseAddress`, timeout, headers, cookies, redirect settings and whether the target is reachable from the application host. If the URL is relative, the site also needs a routable public origin or a configured base address. The status endpoint and application logs contain the saved failure details.

## Documents are skipped

Rebuilds skip content that is unpublished or has no routable URL for the culture being processed. Check publication state, domains and the resolved URL. A URL resolving to `#` is not renderable. Configure `HttpRenderer:BaseAddress` when the site has no usable absolute application URL.

## Rendering context is inactive

`SearchRenderingContext.IsActive` is enabled by the authenticated render header. The built-in renderer sends it automatically. If a custom renderer is used, verify that it uses the configured `RenderRequestHeaderName` and `RenderRequestToken`. On multiple app instances, configure the same token on the renderer and the rendering app.

## Configuration does not apply

Use `Umbraco:Community:RazorSearch`; root-level `RazorSearch` is not read. Explicit arrays replace defaults, and `[]` disables a source group. A missing property uses defaults. Invalid selectors, sources and rendering settings fail runtime validation.

Build the app after package installation to copy and register the JSON schema. Add `"$schema": "appsettings-schema.json"` to the settings file. Check that the core package's `buildTransitive` assets have not been excluded by the app's PackageReference.

Check exclusions separately. `ExcludedContentTypeAliases` and `ExcludeFromSearchPropertyAlias` apply during queueing, indexing and runtime search. Confirm the content type alias and property alias match exactly, and that the opt-out property has a truthy published value for the culture being searched.

Property sources read published values only. Confirm that the property exists on the document type, has a published value in the requested culture and contains text that can be normalized for indexing.

## Duplicate or moved content

If a queue request reports duplicate routes, the same document and culture are already queued or running. Wait for the existing job, or inspect its status, instead of submitting the same request repeatedly.

After moving or renaming a subtree, RazorSearch removes affected snapshots and queues new rendering work. Wait for the new jobs to finish. If old content remains searchable, run a subtree rebuild and inspect the document status for the affected descendants.

## Management request is denied

Rebuild requires publish permissions and access through the user's start nodes. Subtree operations require access to the descendants too. Global rebuilds, queue details and raw HTML require an administrator. Rendering and management run on the dedicated backoffice or a single-server app, not on frontend subscribers.

## Experimental database cannot start

The first beta replaces the unreleased development schema. Use a fresh disposable development database and rebuild snapshots. There is no migration compatibility promise for the earlier unpublished schema.
