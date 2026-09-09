using Umbraco.Community.RazorSearch.Models;

namespace Umbraco.Community.RazorSearch.Searching;

internal static class RazorSearchRequestValidator
{
    internal static string? ResolveCulture(string? culture, IEnumerable<string> configuredCultures)
    {
        if (culture is null) return null;
        return configuredCultures.FirstOrDefault(x => string.Equals(x, culture, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Culture '{culture}' is not a configured Umbraco language.", nameof(culture));
    }

    public static void Validate(Models.RazorSearch search)
    {
        if (string.IsNullOrWhiteSpace(search.Text))
        {
            throw new ArgumentException("Search text must be provided.", nameof(search));
        }

        if (search.Take < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(search), "Take must be greater than zero.");
        }

        if (search.Skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(search), "Skip cannot be negative.");
        }

        if (search.RootKeys.Count == 0)
        {
            return;
        }

        if (search.RootKeys.Any(rootKey => rootKey == Guid.Empty))
        {
            throw new ArgumentException("Root keys cannot contain an empty GUID.", nameof(search));
        }
    }
}
