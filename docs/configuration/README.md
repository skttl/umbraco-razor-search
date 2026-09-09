# Configuration

All settings live under `Umbraco:Community:RazorSearch`. The old root-level `RazorSearch` section is not read.

```json
{
  "$schema": "appsettings-schema.json",
  "Umbraco": {
    "Community": {
      "RazorSearch": {
        "ExcludedContentTypeAliases": ["folder", "search"],
        "ExcludeFromSearchPropertyAlias": "excludeFromSearch",
        "SnapshotExtraction": {
          "TitleSources": [
            { "Type": "property", "Alias": "seoTitle" },
            { "Type": "selector", "Selector": "title" }
          ],
          "BodySources": [{ "Type": "selector", "Selector": "main" }],
          "RemoveSelectors": [".skip-search"]
        }
      }
    }
  }
}
```

## Appsettings schema

Core ships `appsettings-schema.Umbraco.Community.RazorSearch.json`, generated from its options types. A `buildTransitive` props file registers it with Umbraco. Building the consuming app after installation copies it into the project and updates `appsettings-schema.json`. This also works when the Examine companion brings in core transitively.

Use the `$schema` declaration above in each JSON settings file. Editors supporting JSON Schema can provide completion and type validation. Runtime validation still checks selectors, source definitions, URLs and renderer settings. Schema completion allows custom renderer names and other packages under `Umbraco:Community`.

## Extraction

| Setting | Default | Behavior |
| --- | --- | --- |
| `TitleSources` | CSS `title` | First non-empty value |
| `SummarySources` | `meta[name='description']`, attribute `content` | First non-empty value |
| `HeadingSources` | `h1, h2, h3, h4, h5, h6` | Combine matching text |
| `BodySources` | CSS `body` | Combine matching text |
| `RemoveSelectors` | Empty | Remove matching elements before extraction |

A missing source array uses the default. An explicit array replaces it. An empty array disables that source group. For example, `BodySources: []` does not silently revert to `body`. Selecting `main` excludes navigation outside `main`.

A `selector` source requires `Selector` and optionally `Attribute`. A `property` source requires `Alias` and reads published property values in the snapshot's culture. Source types cannot be extended. Invalid or contradictory sources and invalid CSS selectors fail validation. Script, style, noscript and template contents are excluded automatically. Inline HTML preserves word continuity; block elements separate text.

`ExcludedContentTypeAliases` defaults to an empty list. `ExcludeFromSearchPropertyAlias` defaults to null; configure a boolean property alias to opt documents out. Rebuild after changing either option or extraction settings.

## HTTP renderer

Settings below are relative to `Umbraco:Community:RazorSearch`.

| Setting | Default | Purpose |
| --- | --- | --- |
| `DefaultRenderer` | `http` | Registered renderer name |
| `HttpRenderer.BaseAddress` | null | Public origin for relative routes |
| `HttpRenderer.RenderBaseAddress` | null | Internal origin for rendering on the dedicated backoffice |
| `HttpRenderer.Timeout` | `00:00:30` | HTTP request timeout |
| `HttpRenderer.AllowAutoRedirect` | false | Allow at most five redirects within the original public origin |
| `HttpRenderer.Headers` | `X-RazorSearch: true` | Additional request headers |
| `HttpRenderer.Cookies` | Empty | Additional request cookies |
| `RenderRequestHeaderName` | `X-RazorSearch-Render` | Authenticated render-context header |
| `RenderRequestToken` | Generated per app instance | Shared secret for rendering across app instances |

Only successful HTML/XHTML responses become usable snapshots. Cross-origin redirects fail. An internal rendering origin requires an explicit token. Keep it in a secret configuration provider or an environment variable such as `Umbraco__Community__RazorSearch__RenderRequestToken`.

`RenderBaseAddress` changes the network destination while preserving the public host, path and scheme for routing. Public result URLs remain public. See [load balancing](../indexing/README.md).

## Queue

`RenderQueue.Capacity` defaults to 256. It limits pending rendering work; a background rebuild producer waits for capacity without holding the management request open. Publishing during an active render schedules a later generation instead of losing the update.

`RenderQueue.MaxAttempts` defaults to 3, `RetryDelay` to `00:00:02`, and `CompletedJobRetention` to 1000. Only transient failures are retried. A failed attempt preserves the previous usable snapshot. Queued work and job history disappear on restart.

## Highlighting

`HighlightPattern` defaults to `<mark>{0}</mark>` and must contain `{0}`. Extracted text is HTML-encoded before inserting the highlight markup. Configuration is trusted; do not allow site visitors to supply the pattern.

Provider registration is separate. The consuming app must still call `AddSearchCore()` and configure its provider.
