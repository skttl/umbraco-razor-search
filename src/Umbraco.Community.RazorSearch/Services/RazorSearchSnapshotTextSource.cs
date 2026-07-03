using Umbraco.Community.RazorSearch.Configuration;

namespace Umbraco.Community.RazorSearch.Services;

internal enum RazorSearchSnapshotSourceType
{
    Selector,
    Property,
}

internal sealed class RazorSearchSnapshotTextSource(
    RazorSearchSnapshotSourceType sourceType,
    RazorSearchHtmlSelector? selector,
    string? attributeName,
    string? propertyAlias)
{
    public RazorSearchSnapshotSourceType SourceType { get; } = sourceType;

    public RazorSearchHtmlSelector? Selector { get; } = selector;

    public string? AttributeName { get; } = attributeName;

    public string? PropertyAlias { get; } = propertyAlias;

    public static bool TryParse(RazorSearchSnapshotSourceDefinition definition, out RazorSearchSnapshotTextSource? source)
    {
        source = null;

        if (definition is null)
        {
            return false;
        }

        return NormalizeType(definition.Type) switch
        {
            RazorSearchSnapshotSourceTypes.Selector => TryParseSelector(definition, out source),
            RazorSearchSnapshotSourceTypes.Property => TryParseProperty(definition, out source),
            _ => false,
        };
    }

    private static bool TryParseSelector(RazorSearchSnapshotSourceDefinition definition, out RazorSearchSnapshotTextSource? source)
    {
        source = null;

        string selectorValue = definition.Selector?.Trim() ?? string.Empty;
        if (selectorValue.Length == 0)
        {
            return false;
        }

        if (RazorSearchHtmlSelector.TryParse(selectorValue, out RazorSearchHtmlSelector? selector) is false || selector is null)
        {
            return false;
        }

        source = new RazorSearchSnapshotTextSource(
            RazorSearchSnapshotSourceType.Selector,
            selector,
            string.IsNullOrWhiteSpace(definition.Attribute) ? null : definition.Attribute.Trim(),
            null);

        return true;
    }

    private static bool TryParseProperty(RazorSearchSnapshotSourceDefinition definition, out RazorSearchSnapshotTextSource? source)
    {
        source = null;

        string propertyAlias = definition.Alias?.Trim() ?? string.Empty;
        if (propertyAlias.Length == 0)
        {
            return false;
        }

        source = new RazorSearchSnapshotTextSource(
            RazorSearchSnapshotSourceType.Property,
            null,
            null,
            propertyAlias);

        return true;
    }

    private static string NormalizeType(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim() switch
            {
                var x when x.Equals(RazorSearchSnapshotSourceTypes.Selector, StringComparison.OrdinalIgnoreCase) => RazorSearchSnapshotSourceTypes.Selector,
                var x when x.Equals("css", StringComparison.OrdinalIgnoreCase) => RazorSearchSnapshotSourceTypes.Selector,
                var x when x.Equals(RazorSearchSnapshotSourceTypes.Property, StringComparison.OrdinalIgnoreCase) => RazorSearchSnapshotSourceTypes.Property,
                _ => string.Empty,
            };
}
