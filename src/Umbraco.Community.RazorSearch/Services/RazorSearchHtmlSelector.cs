using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Umbraco.Community.RazorSearch.Services;

internal sealed class RazorSearchHtmlSelector(string value)
{
    public string Value { get; } = value;
    public static bool TryParse(string value, out RazorSearchHtmlSelector? selector)
    {
        selector = null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            new HtmlParser().ParseDocument(string.Empty).QuerySelectorAll(value);
            selector = new RazorSearchHtmlSelector(value);
            return true;
        }
        catch (DomException) { return false; }
    }
}
