using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Community.RazorSearch.Configuration;
using Umbraco.Community.RazorSearch.Models;
using Umbraco.Community.RazorSearch.Services;

namespace Umbraco.Community.RazorSearch.Rendering;

internal sealed class HttpRazorSearchRenderer(
    IOptionsMonitor<RazorSearchOptions> options,
    IOptionsMonitor<WebRoutingSettings> webRoutingSettings,
    IRenderRequestTokenProvider renderRequestTokenProvider,
    ILogger<HttpRazorSearchRenderer> logger) : IRazorSearchRenderer
{
    private readonly IOptionsMonitor<RazorSearchOptions> _options = options;
    private readonly IOptionsMonitor<WebRoutingSettings> _webRoutingSettings = webRoutingSettings;
    private readonly IRenderRequestTokenProvider _renderRequestTokenProvider = renderRequestTokenProvider;
    private readonly ILogger<HttpRazorSearchRenderer> _logger = logger;

    public string Name => RazorSearchRendererNames.Http;

    public async Task<RazorSearchRenderResult> RenderAsync(
        RazorSearchRenderJob job,
        CancellationToken cancellationToken = default)
    {
        RazorSearchOptions options = _options.CurrentValue;
        Uri requestUri = ResolveRequestUri(job, options.HttpRenderer, _webRoutingSettings.CurrentValue);

        using HttpClientHandler handler = new()
        {
            AllowAutoRedirect = options.HttpRenderer.AllowAutoRedirect,
        };

        using HttpClient httpClient = new(handler)
        {
            Timeout = options.HttpRenderer.Timeout,
        };

        using HttpRequestMessage request = new(HttpMethod.Get, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        foreach ((string headerName, string headerValue) in options.HttpRenderer.Headers)
        {
            request.Headers.TryAddWithoutValidation(headerName, headerValue);
        }

        if (string.IsNullOrWhiteSpace(options.RenderRequestHeaderName) is false)
        {
            request.Headers.TryAddWithoutValidation(options.RenderRequestHeaderName, _renderRequestTokenProvider.GetToken());
        }
        else
        {
            _logger.LogDebug(
                "RazorSearch HTTP rendering for content {ContentKey} is running without the render-context header because the header name is not configured.",
                job.ContentKey);
        }

        if (options.HttpRenderer.Cookies.Count > 0)
        {
            string cookieHeader = string.Join("; ", options.HttpRenderer.Cookies.Select(x => $"{x.Key}={x.Value}"));
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        }

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        string content = await response.Content.ReadAsStringAsync(cancellationToken);

        return new RazorSearchRenderResult
        {
            JobId = job.Id,
            Success = response.IsSuccessStatusCode,
            Content = content,
            FinalUrl = response.RequestMessage?.RequestUri?.ToString() ?? requestUri.ToString(),
            ContentType = response.Content.Headers.ContentType?.MediaType,
            StatusCode = (int)response.StatusCode,
            ErrorMessage = response.IsSuccessStatusCode
                ? null
                : $"Rendering request returned HTTP {(int)response.StatusCode}.",
            CompletedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private static Uri ResolveRequestUri(
        RazorSearchRenderJob job,
        RazorSearchHttpRendererOptions options,
        WebRoutingSettings webRoutingSettings)
    {
        if (Uri.TryCreate(job.Route, UriKind.Absolute, out Uri? absoluteUri))
        {
            return absoluteUri;
        }

        string? baseAddressValue = string.IsNullOrWhiteSpace(options.BaseAddress)
            ? webRoutingSettings.UmbracoApplicationUrl
            : options.BaseAddress;

        if (string.IsNullOrWhiteSpace(baseAddressValue))
        {
            throw new InvalidOperationException(
                $"Cannot resolve render route '{job.Route}' for content {job.ContentKey} because no absolute route, HttpRenderer.BaseAddress, or WebRouting:UmbracoApplicationUrl was provided.");
        }

        if (Uri.TryCreate(baseAddressValue, UriKind.Absolute, out Uri? baseAddress) is false)
        {
            throw new InvalidOperationException(
                $"The resolved base address '{baseAddressValue}' is not a valid absolute URI.");
        }

        if (Uri.TryCreate(baseAddress, job.Route, out Uri? resolvedUri) is false)
        {
            throw new InvalidOperationException(
                $"Unable to combine base address '{baseAddressValue}' with route '{job.Route}'.");
        }

        return resolvedUri;
    }
}
