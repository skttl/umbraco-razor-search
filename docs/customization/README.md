# Customization

The current package version is intentionally small, but there are still a few useful extension points.

## Build a custom renderer

RazorSearch exposes `IRazorSearchRenderer` so you can replace or supplement the built-in HTTP renderer.

### 1. Implement the renderer

```csharp
using System.Net.Http.Headers;
using Umbraco.Community.RazorSearch.Models;
using Umbraco.Community.RazorSearch.Rendering;

public sealed class InternalHttpRazorSearchRenderer : IRazorSearchRenderer
{
    public string Name => "internal-http";

    public async Task<RazorSearchRenderResult> RenderAsync(
        RazorSearchRenderJob job,
        CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri("https://internal.example.com"),
            Timeout = TimeSpan.FromSeconds(30),
        };

        using var request = new HttpRequestMessage(HttpMethod.Get, job.Route);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        request.Headers.TryAddWithoutValidation("X-RazorSearch-Render", "change-me");

        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        string html = await response.Content.ReadAsStringAsync(cancellationToken);

        return new RazorSearchRenderResult
        {
            JobId = job.Id,
            Success = response.IsSuccessStatusCode,
            Content = html,
            FinalUrl = response.RequestMessage?.RequestUri?.ToString(),
            ContentType = response.Content.Headers.ContentType?.MediaType,
            StatusCode = (int)response.StatusCode,
            ErrorMessage = response.IsSuccessStatusCode
                ? null
                : $"Rendering request returned HTTP {(int)response.StatusCode}.",
            CompletedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
```

### 2. Register it

```csharp
builder.Services.AddSingleton<IRazorSearchRenderer, InternalHttpRazorSearchRenderer>();
```

### 3. Make it the default

```json
{
  "RazorSearch": {
    "DefaultRenderer": "internal-http"
  }
}
```

## Queue a specific renderer manually

If you need per-request control, enqueue render work yourself:

```csharp
using Umbraco.Community.RazorSearch.Models;
using Umbraco.Community.RazorSearch.Services;

public sealed class RazorSearchMaintenanceService(IRazorSearchRenderQueue renderQueue)
{
    public Task QueueAsync(Guid contentKey, string url, string culture, CancellationToken cancellationToken)
    {
        return renderQueue.EnqueueAsync(
            new RazorSearchRenderRequest
            {
                ContentKey = contentKey,
                Route = url,
                Culture = culture,
                Renderer = "internal-http",
                Force = true,
            },
            cancellationToken).AsTask();
    }
}
```

Note that the package management endpoints do not expose renderer selection. They use the default renderer unless you enqueue jobs manually from code.

## Customize highlighting

You can change how match highlighting is rendered in summaries:

```json
{
  "RazorSearch": {
    "HighlightPattern": "<strong>{0}</strong>"
  }
}
```

The pattern must contain `{0}`.

## Customization limits

The following extension points do not exist:

- custom source types beyond `selector` and `property`
- source-specific transforms beyond plain-text normalization
- built-in non-HTTP renderer variants
- custom RazorSearch index field aliases
