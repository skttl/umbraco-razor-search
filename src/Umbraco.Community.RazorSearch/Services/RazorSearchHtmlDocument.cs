using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Umbraco.Community.RazorSearch.Services;

internal sealed class RazorSearchHtmlDocument
{
    private static readonly HashSet<string> BlockElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "aside", "blockquote", "body", "br", "dd", "div", "dl", "dt", "fieldset",
        "figcaption", "figure", "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6", "header", "hr",
        "li", "main", "nav", "ol", "p", "pre", "section", "table", "tbody", "td", "th", "thead", "tr", "ul",
    };
    private RazorSearchHtmlDocument(IDocument document) => Root = document;
    public IDocument Root { get; }
    public static RazorSearchHtmlDocument Parse(string html) => new(new HtmlParser(new HtmlParserOptions { IsScripting = true }).ParseDocument(html));
    public string ExtractText(INode node)
    {
        StringBuilder text = new();
        AppendText(node, text);
        // AngleSharp has already decoded character references; do not decode a second time.
        return string.Join(" ", text.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
    public string ExtractText(IEnumerable<IElement> elements) => string.Join(Environment.NewLine, elements.Select(ExtractText).Where(x => x.Length > 0));
    private static void AppendText(INode node, StringBuilder text)
    {
        if (node is IText value) { text.Append(value.Data); return; }
        bool block = node is IElement element && BlockElements.Contains(element.LocalName);
        if (block) text.Append(' ');
        foreach (INode child in node.ChildNodes) AppendText(child, text);
        if (block) text.Append(' ');
    }
}
