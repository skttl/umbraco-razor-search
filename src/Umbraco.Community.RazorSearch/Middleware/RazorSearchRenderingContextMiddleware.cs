using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Umbraco.Community.RazorSearch.Configuration;
using Umbraco.Community.RazorSearch.Services;

namespace Umbraco.Community.RazorSearch.Middleware;

internal sealed class RazorSearchRenderingContextMiddleware(
    RequestDelegate next,
    IOptions<RazorSearchOptions> options,
    IRenderRequestTokenProvider renderRequestTokenProvider)
{
    private readonly RequestDelegate _next = next;
    private readonly RazorSearchOptions _options = options.Value;
    private readonly IRenderRequestTokenProvider _renderRequestTokenProvider = renderRequestTokenProvider;

    public async Task InvokeAsync(HttpContext httpContext)
    {
        string? configuredHeader = _options.RenderRequestHeaderName;
        string configuredToken = _renderRequestTokenProvider.GetToken();

        if (!string.IsNullOrWhiteSpace(configuredHeader)
            && httpContext.Request.Headers.TryGetValue(configuredHeader, out var tokenValue)
            && StringValues.Equals(tokenValue, configuredToken))
        {
            using IDisposable _ = SearchRenderingContext.Activate();
            await _next(httpContext);
            return;
        }

        await _next(httpContext);
    }
}
