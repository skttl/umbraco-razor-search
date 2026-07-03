# Configuration

Configure RazorSearch under the `RazorSearch` section in `appsettings.json`.

In practice, most installations do not need to add anything here unless you want to change how RazorSearch extracts text, or exclude specific content types or content through a property alias.

## Default values

```json
{
  "RazorSearch": {
    "ExcludedContentTypeAliases": [],
    "ExcludeFromSearchPropertyAlias": null,
    "HighlightPattern": "<mark>{0}</mark>",
    "DefaultRenderer": "http",
    "SnapshotExtraction": {
      "TitleSources": [
        { "Type": "selector", "Selector": "title" }
      ],
      "SummarySources": [
        { "Type": "selector", "Selector": "meta[name='description']", "Attribute": "content" }
      ],
      "HeadingSources": [
        { "Type": "selector", "Selector": "h1" },
        { "Type": "selector", "Selector": "h2" },
        { "Type": "selector", "Selector": "h3" },
        { "Type": "selector", "Selector": "h4" },
        { "Type": "selector", "Selector": "h5" },
        { "Type": "selector", "Selector": "h6" }
      ],
      "BodySources": [
        { "Type": "selector", "Selector": "body" }
      ],
      "RemoveSelectors": []
    },
    "HttpRenderer": {
      "BaseAddress": null,
      "Headers": {
        "X-RazorSearch": "true"
      },
      "Cookies": {},
      "Timeout": "00:00:30",
      "AllowAutoRedirect": true
    },
    "RenderQueue": {
      "Capacity": 256,
      "DeduplicateActiveJobs": true
    }
  }
}
```

This is the package default object as it behaves today. You only need to add settings when you want to override this behavior.

## When to configure something

Most projects can leave the `RazorSearch` section very small, or omit it entirely, as long as the host has already configured Umbraco Search.

You typically only need to add RazorSearch-specific settings when:

- you want to change how snapshot extraction works
- you want to exclude specific content types
- you want to opt content out through a specific property alias
- you need the built-in HTTP renderer to call a specific hostname
- you want full control over the render token used for `SearchRenderingContext.IsActive`

## Provider configuration

This is not part of the `RazorSearch` section, but it is still required.

Your host must configure:

- `AddSearchCore()`
- a concrete provider
- the published content index and searcher used by that provider
- any provider-specific field schema required for the RazorSearch aliases

Without that, RazorSearch cannot return results.

RazorSearch always emits these fixed aliases:

- `RazorSearch_Title`
- `RazorSearch_Summary`
- `RazorSearch_Heading`
- `RazorSearch_Content`

## Configuration sections

### `ExcludedContentTypeAliases`

Use this to exclude one or more content types from RazorSearch.

What it affects:

- queueing
- snapshot generation
- index contribution
- runtime search results

Typical use cases:

- folders or container nodes
- utility document types
- content types that should never appear in site search

Example:

```json
{
  "RazorSearch": {
    "ExcludedContentTypeAliases": ["folderPage", "searchSettings"]
  }
}
```

### `ExcludeFromSearchPropertyAlias`

Use this when editors should be able to opt specific content out of search through a property on the document type.

RazorSearch reads the published property value and treats truthy values as excluded.

Typical use cases:

- a checkbox like `excludeFromSearch`
- SEO or search settings tabs

Example:

```json
{
  "RazorSearch": {
    "ExcludeFromSearchPropertyAlias": "excludeFromSearch"
  }
}
```

### `HighlightPattern`

Controls how matched terms are wrapped in result summaries.

Use this when:

- you want markup other than `<mark>`
- your frontend expects a specific element or class

The value must contain `{0}`.

Example:

```json
{
  "RazorSearch": {
    "HighlightPattern": "<strong class=\"search-hit\">{0}</strong>"
  }
}
```

### `DefaultRenderer`

Controls which registered renderer RazorSearch uses when a queue request does not specify one explicitly.

Most projects can leave this as `http`.

Change it when:

- you register a custom renderer
- you want rebuilds and publish-triggered jobs to use that renderer by default

Tip:

- if you want to build your own renderer, see the custom renderer guide in [../customization/README.md](../customization/README.md)

Example:

```json
{
  "RazorSearch": {
    "DefaultRenderer": "internal-http"
  }
}
```

Examine provider example:

```json
{
  "Umbraco": {
    "CMS": {
      "Search": {
        "Examine": {
          "Fields": [
            { "PropertyName": "RazorSearch_Title", "FieldValues": "TextsR1" },
            { "PropertyName": "RazorSearch_Summary", "FieldValues": "TextsR2" },
            { "PropertyName": "RazorSearch_Heading", "FieldValues": "TextsR2" },
            { "PropertyName": "RazorSearch_Content", "FieldValues": "Texts" }
          ]
        }
      }
    }
  }
}
```

### `SnapshotExtraction`

Controls how RazorSearch builds searchable text from the rendered page and from Umbraco properties.

Use this when:

- you want to prioritize a property over rendered HTML
- you want to pull text from specific HTML regions
- you want to ignore parts of the rendered page

Default behavior:

- title from the rendered `<title>` tag
- summary from `meta[name="description"]`
- headings from rendered `h1` through `h6`
- body from the rendered `<body>` content

#### `TitleSources`

Ordered source definitions for title text.

RazorSearch uses the first non-empty value.

#### `SummarySources`

Ordered source definitions for summary text.

RazorSearch uses the first non-empty value.

#### `HeadingSources`

Ordered source definitions for heading text.

RazorSearch combines all non-empty values and removes duplicates.

#### `BodySources`

Ordered source definitions for body text.

RazorSearch combines all non-empty values and removes duplicates.

#### `RemoveSelectors`

CSS selectors removed before HTML extraction runs.

Useful for:

- navigation
- cookie banners
- alerts
- promo blocks
- repeated UI chrome that should not affect search

Tip:

- if you want to use `SearchRenderingContext.IsActive` to remove or suppress markup during RazorSearch rendering, see [../usage/README.md](../usage/README.md)

#### Source types

| Type | Required field(s) | Meaning |
| --- | --- | --- |
| `selector` | `Selector` | Extracts text or an attribute from matching rendered HTML |
| `property` | `Alias` | Reads the published Umbraco property value for the current content, culture, and segment |

#### Selector sources

Selector sources read from the rendered HTML after `RemoveSelectors` has been applied.

Examples:

```json
{ "Type": "selector", "Selector": "main .page-title" }
{ "Type": "selector", "Selector": "meta[property='og:title']", "Attribute": "content" }
{ "Type": "selector", "Selector": "[data-search-summary]" }
{ "Type": "selector", "Selector": "body" }
```

#### Property sources

Property sources read **published** Umbraco property values from the matching content item and variant.

Examples:

```json
{ "Type": "property", "Alias": "seoTitle" }
{ "Type": "property", "Alias": "seoDescription" }
{ "Type": "property", "Alias": "mainContent" }
```

String property values are normalized to plain text. If a property contains HTML, RazorSearch strips tags before indexing it.

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
      "RemoveSelectors": [".skip-search", "[data-search-ignore='true']"]
    }
  }
}
```

### `HttpRenderer`

Controls the built-in HTTP renderer.

You usually only need this when the default render request cannot reach the site correctly.

#### `BaseAddress`

Set this when your content URLs are not already absolute and routable, and the default Umbraco application URL is not the hostname you want RazorSearch to call.

You typically need it when:

- your content resolves to relative URLs
- domains are not configured in Umbraco
- the renderer must call the site through a specific hostname

If `BaseAddress` is empty, RazorSearch falls back to `WebRouting:UmbracoApplicationUrl`.

#### `Headers`

Additional headers sent with render requests.

The default header `X-RazorSearch: true` is already included.

Use this when:

- the target site expects internal headers
- a reverse proxy or middleware requires a marker header

#### `Cookies`

Cookies sent with render requests.

Use this only when the rendered page depends on a stable cookie value during snapshot generation.

#### `Timeout`

Maximum time allowed for a render request.

Increase it when:

- pages are slow to render
- the renderer must pass through slow upstream systems

#### `AllowAutoRedirect`

Controls whether the HTTP renderer follows redirects.

Leave this enabled in most cases.

Example:

```json
{
  "RazorSearch": {
    "HttpRenderer": {
      "BaseAddress": "https://www.example.com",
      "Headers": {
        "X-RazorSearch": "true",
        "X-Forwarded-Host": "www.example.com"
      },
      "Cookies": {},
      "Timeout": "00:00:45",
      "AllowAutoRedirect": true
    }
  }
}
```

### `RenderQueue`

Controls the in-memory background queue used for snapshot generation.

Most projects can keep the defaults.

#### `Capacity`

Maximum number of queued jobs.

Use a higher value when:

- large rebuilds are common
- many publish events can happen in bursts

Set `0` or less to make the queue unbounded.

#### `DeduplicateActiveJobs`

Prevents duplicate active jobs for the same content and culture.

Leave this enabled in most cases.

Disable it only when you explicitly want overlapping jobs for the same document variant.

Example:

```json
{
  "RazorSearch": {
    "RenderQueue": {
      "Capacity": 512,
      "DeduplicateActiveJobs": true
    }
  }
}
```

### `RenderRequestToken`

Use this only when you want full control over the token value used to activate `SearchRenderingContext.IsActive` during built-in HTTP rendering.

Why it matters:

- the middleware can activate a RazorSearch rendering context from a header token
- the built-in HTTP renderer always sends that header
- if no explicit token is configured, RazorSearch falls back to a deterministic token based on the Umbraco installation id and the host application assembly

In practice, you only need to set this when you want to override the fallback token with a known value.

Example:

```json
{
  "RazorSearch": {
    "RenderRequestToken": "change-me"
  }
}
```

## Active settings summary

| Setting | Purpose |
| --- | --- |
| `ExcludedContentTypeAliases` | Excludes matching content types from queueing, indexing, and runtime search |
| `ExcludeFromSearchPropertyAlias` | Opt-out property alias checked during queueing, indexing, and runtime search |
| `SnapshotExtraction.TitleSources` | Ordered source definitions for extracted title text |
| `SnapshotExtraction.SummarySources` | Ordered source definitions for extracted summary text |
| `SnapshotExtraction.HeadingSources` | Ordered source definitions for extracted heading text |
| `SnapshotExtraction.BodySources` | Ordered source definitions for extracted body text |
| `SnapshotExtraction.RemoveSelectors` | CSS selectors removed before HTML extraction |
| `HighlightPattern` | Template for highlighted summary matches; must contain `{0}` |
| `RenderRequestHeaderName` | Header name used for render-context detection |
| `RenderRequestToken` | Token value for the render-context header |
| `DefaultRenderer` | Default renderer name when queue requests do not specify one |
| `HttpRenderer.BaseAddress` | Fallback base URL for relative routes |
| `HttpRenderer.Headers` | Extra request headers sent by the built-in HTTP renderer |
| `HttpRenderer.Cookies` | Cookies sent by the built-in HTTP renderer |
| `HttpRenderer.Timeout` | HTTP timeout for render requests |
| `HttpRenderer.AllowAutoRedirect` | Whether render requests may follow redirects |
| `RenderQueue.Capacity` | Bounded queue size; `0` or less becomes unbounded |
| `RenderQueue.DeduplicateActiveJobs` | Prevents duplicate active jobs for the same content and culture |

## Fixed search fields

RazorSearch always writes these fields into Umbraco Search:

- `RazorSearch_Title` using `TextsR1`
- `RazorSearch_Summary` using `TextsR2`
- `RazorSearch_Heading` using `TextsR2`
- `RazorSearch_Content` using `Texts`
