using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Community.RazorSearch.Configuration;

namespace Umbraco.Community.RazorSearch.Searching;

internal sealed class RazorSearchContentFilter(IOptionsMonitor<RazorSearchOptions> optionsMonitor) : IRazorSearchContentFilter
{
    public bool IsExcludedContentType(string? contentTypeAlias)
    {
        if (string.IsNullOrWhiteSpace(contentTypeAlias))
        {
            return false;
        }

        return GetExcludedContentTypeAliases().Contains(contentTypeAlias);
    }

    public bool IsExcluded(IPublishedContent content, string? culture = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        return IsExcludedContentType(content.ContentType.Alias)
               || IsExcludedByProperty(content, culture);
    }

    public bool IsExcluded(IContentBase content, string? culture = null, bool published = true)
    {
        ArgumentNullException.ThrowIfNull(content);

        return IsExcludedContentType(content.ContentType.Alias)
               || IsExcludedByProperty(content, culture, published);
    }

    private HashSet<string> GetExcludedContentTypeAliases() => optionsMonitor.CurrentValue.ExcludedContentTypeAliases
        .Where(x => string.IsNullOrWhiteSpace(x) is false)
        .Select(x => x.Trim())
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private string? GetExcludeFromSearchPropertyAlias()
    {
        string? alias = optionsMonitor.CurrentValue.ExcludeFromSearchPropertyAlias;
        return string.IsNullOrWhiteSpace(alias) ? null : alias.Trim();
    }

    private bool IsExcludedByProperty(IPublishedContent content, string? culture)
    {
        string? propertyAlias = GetExcludeFromSearchPropertyAlias();
        if (propertyAlias is null)
        {
            return false;
        }

        IPublishedProperty? property = content.GetProperty(propertyAlias);
        if (property is null)
        {
            return false;
        }

        return IsExcludedValue(property.GetValue(culture, null));
    }

    private bool IsExcludedByProperty(IContentBase content, string? culture, bool published)
    {
        string? propertyAlias = GetExcludeFromSearchPropertyAlias();
        if (propertyAlias is null || content.HasProperty(propertyAlias) is false)
        {
            return false;
        }

        return IsExcludedValue(content.GetValue(propertyAlias, culture, null, published));
    }

    private static bool IsExcludedValue(object? value) => value switch
    {
        null => false,
        bool booleanValue => booleanValue,
        string stringValue => ParseStringValue(stringValue),
        sbyte signedByteValue => signedByteValue != 0,
        byte byteValue => byteValue != 0,
        short shortValue => shortValue != 0,
        ushort unsignedShortValue => unsignedShortValue != 0,
        int intValue => intValue != 0,
        uint unsignedIntValue => unsignedIntValue != 0,
        long longValue => longValue != 0,
        ulong unsignedLongValue => unsignedLongValue != 0,
        float floatValue => Math.Abs(floatValue) > 0,
        double doubleValue => Math.Abs(doubleValue) > 0,
        decimal decimalValue => decimalValue != 0,
        IEnumerable<object> values => values.Any(IsExcludedValue),
        _ => ParseStringValue(value.ToString()),
    };

    private static bool ParseStringValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalizedValue = value.Trim();

        if (bool.TryParse(normalizedValue, out bool booleanValue))
        {
            return booleanValue;
        }

        if (long.TryParse(normalizedValue, out long numericValue))
        {
            return numericValue != 0;
        }

        return normalizedValue.Equals("yes", StringComparison.OrdinalIgnoreCase)
               || normalizedValue.Equals("y", StringComparison.OrdinalIgnoreCase)
               || normalizedValue.Equals("on", StringComparison.OrdinalIgnoreCase);
    }
}
