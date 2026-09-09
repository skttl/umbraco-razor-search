using System.Net;
using System.Net.Http.Headers;
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
    IHttpClientFactory httpClientFactory) : IRazorSearchRenderer
{
    internal const string HttpClientName = "RazorSearch.Rendering";
    public string Name => RazorSearchRendererNames.Http;

    public async Task<RazorSearchRenderResult> RenderAsync(RazorSearchRenderJob job, CancellationToken cancellationToken = default)
    {
        RazorSearchOptions settings = options.CurrentValue;
        Uri publicUri = ResolvePublicUri(job.Route, settings.HttpRenderer, webRoutingSettings.CurrentValue);
        Uri initialPublicUri = publicUri;
        using HttpClient client = httpClientFactory.CreateClient(HttpClientName);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.HttpRenderer.Timeout);
        for (int redirects = 0; ; redirects++)
        {
            Uri destination = ResolveDestination(publicUri, settings.HttpRenderer);
            using HttpRequestMessage request = new(HttpMethod.Get, destination);
            foreach (var header in settings.HttpRenderer.Headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            request.Headers.Host = publicUri.Authority;
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
            request.Headers.TryAddWithoutValidation(settings.RenderRequestHeaderName, renderRequestTokenProvider.GetToken());
            request.Headers.TryAddWithoutValidation(Constants.PublicSchemeHeaderName, publicUri.Scheme);
            if (settings.HttpRenderer.Cookies.Count > 0)
                request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", settings.HttpRenderer.Cookies.Select(x => $"{x.Key}={x.Value}")));
            using HttpResponseMessage response = await client.SendAsync(request, timeout.Token);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                Uri? location = response.Headers.Location;
                if (!settings.HttpRenderer.AllowAutoRedirect) return Failure("Rendering returned a redirect; redirects are disabled.");
                if (redirects >= 5) return Failure("Rendering exceeded the limit of five redirects.");
                if (location is null || !Uri.TryCreate(publicUri, location, out Uri? next)) return Failure("Rendering returned an invalid redirect.");
                if (!SameOrigin(initialPublicUri, next)) return Failure("Rendering redirected outside the original public origin.");
                publicUri = next;
                continue;
            }
            string? mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!response.IsSuccessStatusCode) return Failure($"Rendering request returned HTTP {(int)response.StatusCode}.");
            if (mediaType is not "text/html" and not "application/xhtml+xml") return Failure("Rendering response must have content type text/html or application/xhtml+xml.");
            string html = await response.Content.ReadAsStringAsync(timeout.Token);
            if (string.IsNullOrWhiteSpace(html)) return Failure("Rendering returned an empty HTML response.");
            return new RazorSearchRenderResult
            {
                JobId = job.Id, Success = true, Content = html, FinalUrl = publicUri.ToString(),
                ContentType = mediaType, StatusCode = (int)response.StatusCode, CompletedAtUtc = DateTimeOffset.UtcNow,
            };

            RazorSearchRenderResult Failure(string message) => new()
            {
                JobId = job.Id, Success = false, Content = string.Empty, FinalUrl = publicUri.ToString(),
                ContentType = response.Content.Headers.ContentType?.MediaType, StatusCode = (int)response.StatusCode,
                ErrorMessage = message, CompletedAtUtc = DateTimeOffset.UtcNow,
            };
        }
    }

    internal static Uri ResolvePublicUri(string route, RazorSearchHttpRendererOptions options, WebRoutingSettings routing)
    {
        if (Uri.TryCreate(route, UriKind.Absolute, out Uri? absolute) && absolute.Scheme is "http" or "https") return absolute;
        string? value = string.IsNullOrWhiteSpace(options.BaseAddress) ? routing.UmbracoApplicationUrl : options.BaseAddress;
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? baseUri) || baseUri.Scheme is not "http" and not "https"
            || !Uri.TryCreate(baseUri, route, out Uri? resolved) || resolved.Scheme is not "http" and not "https")
            throw new InvalidOperationException($"Cannot resolve render route '{route}'. Configure {Constants.ConfigurationSection}:HttpRenderer:BaseAddress or Umbraco:CMS:WebRouting:UmbracoApplicationUrl.");
        return resolved;
    }

    internal static Uri ResolveDestination(Uri publicUri, RazorSearchHttpRendererOptions settings) => settings.RenderBaseAddress is { Length: > 0 } address
        ? new Uri(new Uri(address, UriKind.Absolute), publicUri.PathAndQuery) : publicUri;
    private static bool SameOrigin(Uri left, Uri right) => right.UserInfo.Length == 0 && left.Scheme == right.Scheme && left.Host.Equals(right.Host, StringComparison.OrdinalIgnoreCase) && left.Port == right.Port;
}
