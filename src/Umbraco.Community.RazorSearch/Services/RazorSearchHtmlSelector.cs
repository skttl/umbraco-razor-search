namespace Umbraco.Community.RazorSearch.Services;

internal sealed class RazorSearchHtmlSelector(IReadOnlyList<RazorSearchSelectorStep> steps)
{
    public IReadOnlyList<RazorSearchSelectorStep> Steps { get; } = steps;

    public bool Matches(RazorSearchHtmlElement element)
    {
        if (Steps.Count == 0)
        {
            return false;
        }

        if (Steps[^1].Selector.Matches(element) is false)
        {
            return false;
        }

        RazorSearchHtmlElement? current = element;

        for (int index = Steps.Count - 1; index > 0; index--)
        {
            RazorSearchSelectorStep step = Steps[index];

            current = step.Combinator switch
            {
                RazorSearchSelectorCombinator.Child => current?.Parent,
                RazorSearchSelectorCombinator.Descendant => FindMatchingAncestor(current?.Parent, Steps[index - 1].Selector),
                _ => current,
            };

            if (current is null || Steps[index - 1].Selector.Matches(current) is false)
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryParse(string rawSelector, out RazorSearchHtmlSelector? selector)
    {
        selector = null;

        string selectorText = rawSelector.Trim();
        if (selectorText.Length == 0)
        {
            return false;
        }

        List<RazorSearchSelectorStep> steps = [];
        int index = 0;
        RazorSearchSelectorCombinator combinator = RazorSearchSelectorCombinator.None;

        while (index < selectorText.Length)
        {
            SkipWhitespace(selectorText, ref index, ref combinator);

            if (index >= selectorText.Length)
            {
                break;
            }

            if (selectorText[index] == '>')
            {
                combinator = RazorSearchSelectorCombinator.Child;
                index++;
                continue;
            }

            int start = index;
            int bracketDepth = 0;
            char quote = '\0';

            while (index < selectorText.Length)
            {
                char current = selectorText[index];

                if (quote != '\0')
                {
                    if (current == quote)
                    {
                        quote = '\0';
                    }

                    index++;
                    continue;
                }

                if (current is '\'' or '"')
                {
                    quote = current;
                    index++;
                    continue;
                }

                if (current == '[')
                {
                    bracketDepth++;
                    index++;
                    continue;
                }

                if (current == ']')
                {
                    bracketDepth = Math.Max(0, bracketDepth - 1);
                    index++;
                    continue;
                }

                if (bracketDepth == 0 && (char.IsWhiteSpace(current) || current == '>'))
                {
                    break;
                }

                index++;
            }

            string token = selectorText[start..index].Trim();
            if (RazorSearchSimpleSelector.TryParse(token, out RazorSearchSimpleSelector? simpleSelector) is false)
            {
                return false;
            }

            steps.Add(new RazorSearchSelectorStep(simpleSelector!, combinator));
            combinator = RazorSearchSelectorCombinator.Descendant;
        }

        if (steps.Count == 0)
        {
            return false;
        }

        selector = new RazorSearchHtmlSelector(steps);
        return true;
    }

    private static RazorSearchHtmlElement? FindMatchingAncestor(RazorSearchHtmlElement? current, RazorSearchSimpleSelector selector)
    {
        while (current is not null)
        {
            if (selector.Matches(current))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }

    private static void SkipWhitespace(string selector, ref int index, ref RazorSearchSelectorCombinator combinator)
    {
        bool skippedWhitespace = false;

        while (index < selector.Length && char.IsWhiteSpace(selector[index]))
        {
            skippedWhitespace = true;
            index++;
        }

        if (skippedWhitespace && combinator == RazorSearchSelectorCombinator.None)
        {
            combinator = RazorSearchSelectorCombinator.Descendant;
        }
    }
}

internal sealed record RazorSearchSelectorStep(RazorSearchSimpleSelector Selector, RazorSearchSelectorCombinator Combinator);

internal enum RazorSearchSelectorCombinator
{
    None,
    Descendant,
    Child,
}

internal sealed class RazorSearchSimpleSelector(
    string? tagName,
    string? id,
    IReadOnlyList<string> classNames,
    IReadOnlyList<RazorSearchAttributeSelector> attributes)
{
    public bool Matches(RazorSearchHtmlElement element)
    {
        if (string.IsNullOrWhiteSpace(tagName) is false
            && string.Equals(tagName, "*", StringComparison.Ordinal) is false
            && string.Equals(element.Name, tagName, StringComparison.OrdinalIgnoreCase) is false)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(id) is false
            && element.Attributes.TryGetValue("id", out string? elementId) is false)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(id) is false
            && string.Equals(element.Attributes["id"], id, StringComparison.Ordinal) is false)
        {
            return false;
        }

        if (classNames.Count > 0)
        {
            element.Attributes.TryGetValue("class", out string? classAttribute);
            HashSet<string> classes = (classAttribute ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.Ordinal);

            if (classNames.Any(className => classes.Contains(className) is false))
            {
                return false;
            }
        }

        return attributes.All(attribute => attribute.Matches(element));
    }

    public static bool TryParse(string token, out RazorSearchSimpleSelector? selector)
    {
        selector = null;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string? tagName = null;
        string? id = null;
        List<string> classNames = [];
        List<RazorSearchAttributeSelector> attributes = [];

        int index = 0;
        if (token[index] == '*')
        {
            tagName = "*";
            index++;
        }
        else if (char.IsLetter(token[index]))
        {
            int start = index;
            while (index < token.Length && IsNameCharacter(token[index]))
            {
                index++;
            }

            tagName = token[start..index];
        }

        while (index < token.Length)
        {
            char current = token[index];

            if (current == '#')
            {
                index++;
                int start = index;
                while (index < token.Length && IsNameCharacter(token[index]))
                {
                    index++;
                }

                if (start == index)
                {
                    return false;
                }

                id = token[start..index];
                continue;
            }

            if (current == '.')
            {
                index++;
                int start = index;
                while (index < token.Length && IsNameCharacter(token[index]))
                {
                    index++;
                }

                if (start == index)
                {
                    return false;
                }

                classNames.Add(token[start..index]);
                continue;
            }

            if (current == '[')
            {
                int end = FindAttributeEnd(token, index);
                if (end < 0)
                {
                    return false;
                }

                if (RazorSearchAttributeSelector.TryParse(token[(index + 1)..end], out RazorSearchAttributeSelector? attribute) is false)
                {
                    return false;
                }

                attributes.Add(attribute!);
                index = end + 1;
                continue;
            }

            return false;
        }

        selector = new RazorSearchSimpleSelector(tagName, id, classNames, attributes);
        return true;
    }

    private static int FindAttributeEnd(string token, int start)
    {
        char quote = '\0';

        for (int index = start + 1; index < token.Length; index++)
        {
            char current = token[index];

            if (quote != '\0')
            {
                if (current == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (current is '\'' or '"')
            {
                quote = current;
                continue;
            }

            if (current == ']')
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsNameCharacter(char value)
        => char.IsLetterOrDigit(value) || value is '-' or '_' or ':';
}

internal sealed class RazorSearchAttributeSelector(string name, string? operation, string? expectedValue)
{
    public bool Matches(RazorSearchHtmlElement element)
    {
        if (element.Attributes.TryGetValue(name, out string? actualValue) is false)
        {
            return false;
        }

        return operation switch
        {
            null => true,
            "=" => string.Equals(actualValue, expectedValue, StringComparison.Ordinal),
            "*=" => actualValue.Contains(expectedValue ?? string.Empty, StringComparison.Ordinal),
            "^=" => actualValue.StartsWith(expectedValue ?? string.Empty, StringComparison.Ordinal),
            "$=" => actualValue.EndsWith(expectedValue ?? string.Empty, StringComparison.Ordinal),
            "~=" => actualValue.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(expectedValue ?? string.Empty, StringComparer.Ordinal),
            "|=" => string.Equals(actualValue, expectedValue, StringComparison.Ordinal)
                || actualValue.StartsWith($"{expectedValue}-", StringComparison.Ordinal),
            _ => false,
        };
    }

    public static bool TryParse(string rawValue, out RazorSearchAttributeSelector? selector)
    {
        selector = null;

        string value = rawValue.Trim();
        if (value.Length == 0)
        {
            return false;
        }

        string[] operators = ["*=", "^=", "$=", "~=", "|=", "="];

        foreach (string candidate in operators)
        {
            int operatorIndex = value.IndexOf(candidate, StringComparison.Ordinal);
            if (operatorIndex < 0)
            {
                continue;
            }

            string name = value[..operatorIndex].Trim();
            string expected = value[(operatorIndex + candidate.Length)..].Trim().Trim('\'', '"');
            if (name.Length == 0)
            {
                return false;
            }

            selector = new RazorSearchAttributeSelector(name, candidate, expected);
            return true;
        }

        selector = new RazorSearchAttributeSelector(value, null, null);
        return true;
    }
}
