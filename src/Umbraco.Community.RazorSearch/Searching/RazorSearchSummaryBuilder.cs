using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Umbraco.Community.RazorSearch.Searching;

internal static partial class RazorSearchSummaryBuilder
{
    private const int SummaryLength = 220;
    private const int SummaryPadding = 70;
    private const string DefaultHighlightPattern = "<mark>{0}</mark>";

    public static string Build(string text, IReadOnlyCollection<string> terms, string highlightPattern)
    {
        string normalizedText = NormalizeWhitespaceRegex().Replace(text, " ").Trim();
        if (normalizedText.Length == 0)
        {
            return string.Empty;
        }

        int firstMatchIndex = FindFirstMatchIndex(normalizedText, terms);
        int startIndex = Math.Max(0, firstMatchIndex - SummaryPadding);
        int maxLength = Math.Min(SummaryLength, normalizedText.Length - startIndex);
        string snippet = normalizedText.Substring(startIndex, maxLength).Trim();

        if (startIndex > 0)
        {
            snippet = "..." + snippet;
        }

        if (startIndex + maxLength < normalizedText.Length)
        {
            snippet += "...";
        }

        return HighlightTerms(snippet, terms, NormalizeHighlightPattern(highlightPattern));
    }

    private static int FindFirstMatchIndex(string text, IReadOnlyCollection<string> terms)
    {
        int firstMatchIndex = 0;

        foreach (string term in terms)
        {
            int index = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (index >= 0 && (firstMatchIndex == 0 || index < firstMatchIndex))
            {
                firstMatchIndex = index;
            }
        }

        return firstMatchIndex;
    }

    private static string HighlightTerms(string text, IReadOnlyCollection<string> terms, string highlightPattern)
    {
        if (terms.Count == 0)
        {
            return WebUtility.HtmlEncode(text);
        }

        Match[] matches = terms
            .SelectMany(term => string.IsNullOrWhiteSpace(term)
                ? []
                : Regex.Matches(text, Regex.Escape(term), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Cast<Match>())
            .OrderBy(match => match.Index)
            .ThenByDescending(match => match.Length)
            .ToArray();

        if (matches.Length == 0)
        {
            return WebUtility.HtmlEncode(text);
        }

        var builder = new StringBuilder(text.Length + 32);
        int currentIndex = 0;

        foreach (Match match in matches)
        {
            if (match.Index < currentIndex)
            {
                continue;
            }

            builder.Append(WebUtility.HtmlEncode(text[currentIndex..match.Index]));
            builder.Append(highlightPattern.Replace("{0}", WebUtility.HtmlEncode(match.Value), StringComparison.Ordinal));
            currentIndex = match.Index + match.Length;
        }

        builder.Append(WebUtility.HtmlEncode(text[currentIndex..]));
        return builder.ToString();
    }

    private static string NormalizeHighlightPattern(string highlightPattern)
        => string.IsNullOrWhiteSpace(highlightPattern) || highlightPattern.Contains("{0}", StringComparison.Ordinal) is false
            ? DefaultHighlightPattern
            : highlightPattern;

    [GeneratedRegex(@"\s+")]
    private static partial Regex NormalizeWhitespaceRegex();
}
