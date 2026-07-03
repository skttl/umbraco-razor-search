using System.Text.RegularExpressions;

namespace Umbraco.Community.RazorSearch.Services;

internal sealed partial class RazorSearchHtmlDocument
{
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area",
        "base",
        "br",
        "col",
        "embed",
        "hr",
        "img",
        "input",
        "link",
        "meta",
        "param",
        "source",
        "track",
        "wbr",
    };

    public RazorSearchHtmlElement Root { get; } = new("#document", []);

    public static RazorSearchHtmlDocument Parse(string html)
    {
        RazorSearchHtmlDocument document = new();
        Stack<RazorSearchHtmlElement> stack = new();
        stack.Push(document.Root);

        int lastIndex = 0;

        foreach (Match match in HtmlTokenRegex().Matches(html))
        {
            if (match.Index > lastIndex)
            {
                AppendText(stack.Peek(), html[lastIndex..match.Index]);
            }

            string token = match.Value;
            lastIndex = match.Index + match.Length;

            if (token.StartsWith("<!--", StringComparison.Ordinal))
            {
                continue;
            }

            if (token.StartsWith("</", StringComparison.Ordinal))
            {
                string closingTagName = ParseTagName(token);
                if (closingTagName.Length == 0)
                {
                    continue;
                }

                while (stack.Count > 1 && string.Equals(stack.Peek().Name, closingTagName, StringComparison.OrdinalIgnoreCase) is false)
                {
                    stack.Pop();
                }

                if (stack.Count > 1)
                {
                    stack.Pop();
                }

                continue;
            }

            string tagName = ParseTagName(token);
            if (tagName.Length == 0)
            {
                continue;
            }

            RazorSearchHtmlElement element = new(tagName, ParseAttributes(token));
            stack.Peek().Children.Add(element);
            element.Parent = stack.Peek();

            if (token.EndsWith("/>", StringComparison.Ordinal) || VoidElements.Contains(tagName))
            {
                continue;
            }

            stack.Push(element);
        }

        if (lastIndex < html.Length)
        {
            AppendText(stack.Peek(), html[lastIndex..]);
        }

        return document;
    }

    public IEnumerable<RazorSearchHtmlElement> Descendants(string tagName)
        => Root.Descendants().Where(x => string.Equals(x.Name, tagName, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<RazorSearchHtmlElement> Descendants(ISet<string> tagNames)
        => Root.Descendants().Where(x => tagNames.Contains(x.Name));

    public string ExtractText(RazorSearchHtmlElement? element)
        => element is null ? string.Empty : ExtractTextNodes(element).ToJoinedText();

    public string ExtractText(IEnumerable<RazorSearchHtmlElement> elements)
        => elements
            .Select(element => ExtractTextNodes(element).ToJoinedText())
            .Where(x => string.IsNullOrWhiteSpace(x) is false)
            .ToJoinedText(Environment.NewLine);

    private static IEnumerable<string> ExtractTextNodes(RazorSearchHtmlElement element)
    {
        if (element.IsRemoved)
        {
            yield break;
        }

        foreach (RazorSearchHtmlNode child in element.Children)
        {
            if (child is RazorSearchHtmlText text)
            {
                if (string.IsNullOrWhiteSpace(text.Value) is false)
                {
                    yield return text.Value;
                }

                continue;
            }

            if (child is RazorSearchHtmlElement childElement)
            {
                foreach (string childText in ExtractTextNodes(childElement))
                {
                    yield return childText;
                }
            }
        }
    }

    private static void AppendText(RazorSearchHtmlElement parent, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        parent.Children.Add(new RazorSearchHtmlText(value) { Parent = parent });
    }

    private static string ParseTagName(string token)
    {
        Match match = TagNameRegex().Match(token);
        return match.Success ? match.Groups["name"].Value : string.Empty;
    }

    private static Dictionary<string, string> ParseAttributes(string token)
    {
        Dictionary<string, string> attributes = new(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in AttributeRegex().Matches(token))
        {
            string attributeName = match.Groups["name"].Value;
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                continue;
            }

            string value = match.Groups["doubleQuoted"].Success
                ? match.Groups["doubleQuoted"].Value
                : match.Groups["singleQuoted"].Success
                    ? match.Groups["singleQuoted"].Value
                    : match.Groups["unquoted"].Success
                        ? match.Groups["unquoted"].Value
                        : string.Empty;

            attributes[attributeName] = value;
        }

        return attributes;
    }

    [GeneratedRegex("<!--.*?-->|</?[A-Za-z][^>]*?>", RegexOptions.Singleline)]
    private static partial Regex HtmlTokenRegex();

    [GeneratedRegex("^</?\\s*(?<name>[A-Za-z][A-Za-z0-9:-]*)", RegexOptions.Singleline)]
    private static partial Regex TagNameRegex();

    [GeneratedRegex("(?<name>[A-Za-z_:][A-Za-z0-9_:\\-.]*)(?:\\s*=\\s*(?:\"(?<doubleQuoted>[^\"]*)\"|'(?<singleQuoted>[^']*)'|(?<unquoted>[^\\s\"'=<>`]+)))?", RegexOptions.Singleline)]
    private static partial Regex AttributeRegex();
}

internal abstract class RazorSearchHtmlNode
{
    public RazorSearchHtmlElement? Parent { get; set; }
}

internal sealed class RazorSearchHtmlElement(string name, Dictionary<string, string> attributes) : RazorSearchHtmlNode
{
    public string Name { get; } = name;

    public Dictionary<string, string> Attributes { get; } = attributes;

    public List<RazorSearchHtmlNode> Children { get; } = [];

    public bool IsRemoved { get; private set; }

    public IEnumerable<RazorSearchHtmlElement> Descendants()
    {
        foreach (RazorSearchHtmlNode child in Children)
        {
            if (child is not RazorSearchHtmlElement element)
            {
                continue;
            }

            yield return element;

            foreach (RazorSearchHtmlElement descendant in element.Descendants())
            {
                yield return descendant;
            }
        }
    }

    public void MarkRemoved()
    {
        IsRemoved = true;
    }
}

internal sealed class RazorSearchHtmlText(string value) : RazorSearchHtmlNode
{
    public string Value { get; } = value;
}

internal static class RazorSearchHtmlDocumentExtensions
{
    public static string ToJoinedText(this IEnumerable<string> values, string separator = " ")
        => RazorSearchSnapshotTextSanitizer.NormalizeWhitespace(string.Join(separator, values));
}
