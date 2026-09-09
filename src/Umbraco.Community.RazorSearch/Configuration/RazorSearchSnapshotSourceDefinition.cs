using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Umbraco.Community.RazorSearch.Configuration;

public sealed class RazorSearchSnapshotSourceDefinition
{
    /// <summary>Source kind: selector for HTML/CSS extraction, or property for an Umbraco property.</summary>
    [RegularExpression("^(selector|property)$"), DefaultValue("selector")]
    public string Type { get; set; } = RazorSearchSnapshotSourceTypes.Selector;

    /// <summary>CSS selector, required when Type is selector.</summary>
    public string? Selector { get; set; }

    /// <summary>Optional attribute to extract instead of the selected element's text.</summary>
    public string? Attribute { get; set; }

    /// <summary>Published property alias, required when Type is property.</summary>
    public string? Alias { get; set; }

    internal RazorSearchSnapshotSourceDefinition Normalize()
        => new()
        {
            Type = NormalizeValue(Type) ?? RazorSearchSnapshotSourceTypes.Selector,
            Selector = NormalizeValue(Selector),
            Attribute = NormalizeValue(Attribute),
            Alias = NormalizeValue(Alias),
        };

    internal static RazorSearchSnapshotSourceDefinition SelectorSource(string selector, string? attribute = null)
        => new()
        {
            Type = RazorSearchSnapshotSourceTypes.Selector,
            Selector = selector,
            Attribute = attribute,
        };

    internal static RazorSearchSnapshotSourceDefinition PropertySource(string alias)
        => new()
        {
            Type = RazorSearchSnapshotSourceTypes.Property,
            Alias = alias,
        };

    private static string? NormalizeValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class RazorSearchSnapshotSourceTypes
{
    public const string Selector = "selector";

    public const string Property = "property";
}
