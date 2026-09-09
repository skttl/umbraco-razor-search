# Customization

## Custom renderers

Implement `IRazorSearchRenderer` and register it through dependency injection. Renderers return HTML and attempt metadata; the queue owns persistence, extraction, retry decisions and freshness checks.

```csharp
using Umbraco.Community.RazorSearch.Models;
using Umbraco.Community.RazorSearch.Rendering;

public sealed class CustomRenderer(IHttpClientFactory clients) : IRazorSearchRenderer
{
    public string Name => "custom";

    public async Task<RazorSearchRenderResult> RenderAsync(
        RazorSearchRenderJob job,
        CancellationToken cancellationToken = default)
    {
        using HttpClient client = clients.CreateClient("CustomSearchRenderer");
        using HttpResponseMessage response = await client.GetAsync(job.Route, cancellationToken);
        string? mediaType = response.Content.Headers.ContentType?.MediaType;
        bool success = response.IsSuccessStatusCode && mediaType == "text/html";
        return new RazorSearchRenderResult
        {
            JobId = job.Id,
            Success = success,
            Content = success ? await response.Content.ReadAsStringAsync(cancellationToken) : "",
            FinalUrl = job.Route,
            ContentType = mediaType,
            StatusCode = (int)response.StatusCode,
            ErrorMessage = success ? null : "Expected a successful HTML response.",
            CompletedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
```

Register before building the app:

```csharp
builder.Services.AddHttpClient("CustomSearchRenderer")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<IRazorSearchRenderer, CustomRenderer>();
```

Select the renderer:

```json
{
  "Umbraco": {
    "Community": {
      "RazorSearch": {
        "DefaultRenderer": "custom"
      }
    }
  }
}
```

The example does not activate `SearchRenderingContext`. If a custom renderer needs that feature, use the package's `IRenderRequestTokenProvider` and configured header name. Avoid forwarding render credentials to an untrusted origin. Prefer the built-in HTTP renderer with `HttpRenderer.RenderBaseAddress` when only the destination needs changing.

## Manual queueing

```csharp
using Umbraco.Community.RazorSearch.Models;
using Umbraco.Community.RazorSearch.Services;

public sealed class Maintenance(IRazorSearchRenderQueue queue)
{
    public async Task QueueAsync(Guid contentKey, string publicUrl, string? culture,
        CancellationToken cancellationToken)
    {
        await queue.EnqueueAsync(new RazorSearchRenderRequest
        {
            ContentKey = contentKey,
            Route = publicUrl,
            Culture = culture,
            Renderer = "custom"
        }, cancellationToken);
    }
}
```

The worker resolves the current published route before execution. The request URL cannot force an obsolete route or unpublished document into a snapshot. A different renderer replaces the same document/culture snapshot rather than adding a parallel variant. Management operations use the configured default renderer.

## Other extension points

Configure CSS/property extraction and trusted highlight markup through [configuration](../configuration/README.md). Custom source types, transforms, raw provider result fields, segments and index aliases are outside the public beta contract. Core remains provider-agnostic.
