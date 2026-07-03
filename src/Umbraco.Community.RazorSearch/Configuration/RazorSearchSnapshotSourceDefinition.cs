namespace Umbraco.Community.RazorSearch.Configuration;

public sealed class RazorSearchSnapshotSourceDefinition
{
    public string Type { get; set; } = RazorSearchSnapshotSourceTypes.Selector;

    public string? Selector { get; set; }

    public string? Attribute { get; set; }

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
