using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Umbraco.Community.RazorSearch.Services;

namespace Umbraco.Community.RazorSearch.Configuration;

internal sealed class RazorSearchOptionsValidator : IValidateOptions<RazorSearchOptions>
{
    public ValidateOptionsResult Validate(string? name, RazorSearchOptions options)
    {
        List<string> errors = [];
        void Error(string message) => errors.Add($"{Constants.ConfigurationSection}: {message}");
        if (string.IsNullOrWhiteSpace(options.DefaultRenderer)) Error("DefaultRenderer must name a registered renderer.");
        if (!IsHeaderName(options.RenderRequestHeaderName)) Error("RenderRequestHeaderName must be a valid HTTP header name.");
        if (options.HighlightPattern is null || !options.HighlightPattern.Contains("{0}", StringComparison.Ordinal)) Error("HighlightPattern must contain {0}.");
        else { try { _ = string.Format(options.HighlightPattern, "text"); } catch (FormatException) { Error("HighlightPattern is not a valid composite format string."); } }
        if (options.ExcludedContentTypeAliases is null) Error("ExcludedContentTypeAliases cannot be null.");
        if (options.RenderRequestToken?.Any(char.IsControl) == true) Error("RenderRequestToken cannot contain control characters.");
        if (options.HttpRenderer is not { } http) Error("HttpRenderer cannot be null.");
        else
        {
            if (http.Timeout <= TimeSpan.Zero || http.Timeout.TotalMilliseconds > int.MaxValue) Error("HttpRenderer:Timeout must be positive and within the HttpClient timeout limit.");
            if (http.BaseAddress is not null && !IsHttpAddress(http.BaseAddress, false)) Error("HttpRenderer:BaseAddress must be an absolute HTTP(S) URL without credentials or fragment.");
            if (http.RenderBaseAddress is not null && !IsHttpAddress(http.RenderBaseAddress, true)) Error("HttpRenderer:RenderBaseAddress must be an HTTP(S) origin without path, query, credentials or fragment.");
            if (http.RenderBaseAddress is not null && string.IsNullOrWhiteSpace(options.RenderRequestToken)) Error("RenderRequestToken is required with HttpRenderer:RenderBaseAddress.");
            if (http.Headers is null || http.Headers.Any(x => !IsHeaderName(x.Key) || x.Value.Any(char.IsControl))) Error("HttpRenderer:Headers contains an invalid header.");
            else if (http.Headers.Keys.Any(x => x.Equals("Host", StringComparison.OrdinalIgnoreCase) || x.Equals(Constants.PublicSchemeHeaderName, StringComparison.OrdinalIgnoreCase) || x.Equals(options.RenderRequestHeaderName, StringComparison.OrdinalIgnoreCase))) Error("HttpRenderer:Headers cannot override the public host or render authentication headers.");
            if (http.Cookies is null || http.Cookies.Any(x => !IsHeaderName(x.Key) || x.Value.Any(c => char.IsControl(c) || c is ';' or ','))) Error("HttpRenderer:Cookies contains an invalid cookie.");
        }
        if (options.RenderQueue is not { } queue) Error("RenderQueue cannot be null.");
        else
        {
            if (queue.Capacity is < 1 or > 100000) Error("RenderQueue:Capacity must be between 1 and 100000.");
            if (queue.MaxAttempts is < 1 or > 10) Error("RenderQueue:MaxAttempts must be between 1 and 10.");
            if (queue.RetryDelay <= TimeSpan.Zero || queue.RetryDelay > TimeSpan.FromHours(1)) Error("RenderQueue:RetryDelay must be positive and at most one hour.");
            if (queue.CompletedJobRetention is < 0 or > 100000) Error("RenderQueue:CompletedJobRetention must be between 0 and 100000.");
        }
        if (options.SnapshotExtraction is not { } extraction) Error("SnapshotExtraction cannot be null.");
        else
        {
            ValidateSources(extraction.TitleSources, "TitleSources", Error);
            ValidateSources(extraction.SummarySources, "SummarySources", Error);
            ValidateSources(extraction.HeadingSources, "HeadingSources", Error);
            ValidateSources(extraction.BodySources, "BodySources", Error);
            if (extraction.RemoveSelectors is null || extraction.RemoveSelectors.Any(x => !RazorSearchHtmlSelector.TryParse(x, out _))) Error("SnapshotExtraction:RemoveSelectors contains an invalid CSS selector.");
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static void ValidateSources(RazorSearchSnapshotSourceDefinition[]? sources, string name, Action<string> error)
    {
        if (sources is null) { error($"SnapshotExtraction:{name} cannot be null; use [] to disable it."); return; }
        for (int i = 0; i < sources.Length; i++)
            if (!RazorSearchSnapshotTextSource.TryParse(sources[i], out _)) error($"SnapshotExtraction:{name}:{i} must have Type selector with a valid Selector, or Type property with an Alias.");
    }
    private static bool IsHeaderName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        using HttpRequestMessage request = new();
        try { request.Headers.Add(name, "value"); return true; } catch (Exception ex) when (ex is FormatException or InvalidOperationException) { return false; }
    }
    private static bool IsHttpAddress(string value, bool originOnly) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https" && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0
        && (!originOnly || (uri.AbsolutePath == "/" && uri.Query.Length == 0));
}
