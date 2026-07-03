using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Community.RazorSearch.Configuration;

namespace Umbraco.Community.RazorSearch.Services;

internal static partial class RazorSearchSnapshotTextSanitizer
{
    public static RazorSearchSnapshotContent ExtractSnapshotContent(
        string html,
        string sourceUrl,
        string? finalUrl,
        RazorSearchSnapshotExtractionContext? context = null)
    {
        RazorSearchSnapshotExtractionSettings settings = RazorSearchSnapshotExtractionSettingsStore.Current;
        RazorSearchHtmlDocument document = CreateDocument(html, settings);
        string resolvedFinalUrl = string.IsNullOrWhiteSpace(finalUrl) ? sourceUrl : finalUrl;
        string titleText = ExtractTitleText(document, settings, context);
        string summaryText = ExtractMetaDescription(document, settings, context);
        string headingText = ExtractHeadingText(document, settings, context);
        string bodyText = ExtractBodyText(document, settings, context);
        string combinedText = string.Join(
            Environment.NewLine,
            new[] { titleText, summaryText, headingText, bodyText }.Where(x => string.IsNullOrWhiteSpace(x) is false));

        return new RazorSearchSnapshotContent
        {
            TitleText = titleText,
            SummaryText = summaryText,
            HeadingText = headingText,
            BodyText = bodyText,
            CombinedText = combinedText,
            SnapshotHtml = html,
            FinalUrl = resolvedFinalUrl,
            ContentHash = ComputeChecksum(string.Join("\n||\n", new[] { titleText, summaryText, headingText, bodyText, resolvedFinalUrl })),
        };
    }

    public static string ExtractPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        RazorSearchHtmlDocument document = CreateDocument(html, RazorSearchSnapshotExtractionSettings.Default);
        return document.ExtractText(document.Root);
    }

    public static string ExtractTitleText(string html)
        => ExtractTitleText(CreateDocument(html, RazorSearchSnapshotExtractionSettings.Default), RazorSearchSnapshotExtractionSettings.Default, null);

    public static string ExtractMetaDescription(string html)
    {
        RazorSearchSnapshotExtractionSettings settings = RazorSearchSnapshotExtractionSettings.Default;
        return ExtractMetaDescription(CreateDocument(html, settings), settings, null);
    }

    public static string ExtractHeadingText(string html)
        => ExtractHeadingText(CreateDocument(html, RazorSearchSnapshotExtractionSettings.Default), RazorSearchSnapshotExtractionSettings.Default, null);

    public static string ExtractBodyText(string html)
        => ExtractBodyText(CreateDocument(html, RazorSearchSnapshotExtractionSettings.Default), RazorSearchSnapshotExtractionSettings.Default, null);

    public static string ComputeChecksum(string content)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    [GeneratedRegex("<(script|style|noscript)[^>]*>.*?</\\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptStyleRegex();

    [GeneratedRegex("\\s+", RegexOptions.Singleline)]
    private static partial Regex WhitespaceRegex();

    internal static string NormalizeWhitespace(string value)
        => WhitespaceRegex().Replace(WebUtility.HtmlDecode(value), " ").Trim();

    private static RazorSearchHtmlDocument CreateDocument(string html, RazorSearchSnapshotExtractionSettings settings)
    {
        string sanitized = ScriptStyleRegex().Replace(html, " ");
        RazorSearchHtmlDocument document = RazorSearchHtmlDocument.Parse(sanitized);

        foreach (string removeSelector in settings.RemoveSelectors)
        {
            if (RazorSearchHtmlSelector.TryParse(removeSelector, out RazorSearchHtmlSelector? selector) is false || selector is null)
            {
                continue;
            }

            foreach (RazorSearchHtmlElement element in document.Root.Descendants().Where(selector.Matches))
            {
                element.MarkRemoved();
            }
        }

        return document;
    }

    private static string ExtractTitleText(
        RazorSearchHtmlDocument document,
        RazorSearchSnapshotExtractionSettings settings,
        RazorSearchSnapshotExtractionContext? context)
        => ExtractFirstAvailableValue(document, settings.TitleSources, context);

    private static string ExtractMetaDescription(
        RazorSearchHtmlDocument document,
        RazorSearchSnapshotExtractionSettings settings,
        RazorSearchSnapshotExtractionContext? context)
        => ExtractFirstAvailableValue(document, settings.SummarySources, context);

    private static string ExtractHeadingText(
        RazorSearchHtmlDocument document,
        RazorSearchSnapshotExtractionSettings settings,
        RazorSearchSnapshotExtractionContext? context)
        => ExtractCombinedValues(document, settings.HeadingSources, context);

    private static string ExtractBodyText(
        RazorSearchHtmlDocument document,
        RazorSearchSnapshotExtractionSettings settings,
        RazorSearchSnapshotExtractionContext? context)
        => ExtractCombinedValues(document, settings.BodySources, context);

    private static string ExtractFirstAvailableValue(
        RazorSearchHtmlDocument document,
        IEnumerable<RazorSearchSnapshotTextSource> configuredSources,
        RazorSearchSnapshotExtractionContext? context)
    {
        foreach (RazorSearchSnapshotTextSource configuredSource in configuredSources)
        {
            string value = ExtractFromSource(document, configuredSource, context);
            if (string.IsNullOrWhiteSpace(value) is false)
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string ExtractCombinedValues(
        RazorSearchHtmlDocument document,
        IEnumerable<RazorSearchSnapshotTextSource> configuredSources,
        RazorSearchSnapshotExtractionContext? context)
        => configuredSources
            .Select(configuredSource => ExtractFromSource(document, configuredSource, context))
            .Where(x => string.IsNullOrWhiteSpace(x) is false)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            .ToJoinedText(Environment.NewLine);

    private static string ExtractFromSource(
        RazorSearchHtmlDocument document,
        RazorSearchSnapshotTextSource source,
        RazorSearchSnapshotExtractionContext? context)
    {
        if (source.SourceType == RazorSearchSnapshotSourceType.Property)
        {
            return ExtractPropertyValue(context, source.PropertyAlias);
        }

        if (source.SourceType != RazorSearchSnapshotSourceType.Selector || source.Selector is null)
        {
            return string.Empty;
        }

        RazorSearchHtmlElement[] matches = document.Root.Descendants()
            .Where(x => x.IsRemoved is false)
            .Where(source.Selector.Matches)
            .ToArray();

        if (matches.Length == 0)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(source.AttributeName)
            ? document.ExtractText(matches)
            : matches
                .Select(x => x.Attributes.TryGetValue(source.AttributeName, out string? attributeValue) ? NormalizeWhitespace(attributeValue) : string.Empty)
                .Where(x => string.IsNullOrWhiteSpace(x) is false)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
                .ToJoinedText(Environment.NewLine);
    }

    private static string ExtractPropertyValue(RazorSearchSnapshotExtractionContext? context, string? propertyAlias)
    {
        if (context?.Content is null || string.IsNullOrWhiteSpace(propertyAlias))
        {
            return string.Empty;
        }

        IPublishedProperty? property = context.Content.GetProperty(propertyAlias);
        return property is null
            ? string.Empty
            : NormalizePropertyValue(property.GetValue(context.Culture, context.Segment));
    }

    private static string NormalizePropertyValue(object? value)
    {
        switch (value)
        {
            case null:
                return string.Empty;
            case string stringValue:
                return NormalizeTextValue(stringValue);
            case IEnumerable<string> stringValues:
                return stringValues
                    .Select(NormalizeTextValue)
                    .Where(x => string.IsNullOrWhiteSpace(x) is false)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                    .ToJoinedText(Environment.NewLine);
            case System.Collections.IEnumerable values:
                return values.Cast<object?>()
                    .Select(NormalizePropertyValue)
                    .Where(x => string.IsNullOrWhiteSpace(x) is false)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                    .ToJoinedText(Environment.NewLine);
            default:
                return NormalizeTextValue(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        }
    }

    private static string NormalizeTextValue(string value)
        => LooksLikeHtml(value)
            ? ExtractPlainText(value)
            : NormalizeWhitespace(value);

    private static bool LooksLikeHtml(string value)
        => value.Contains('<') && value.Contains('>');
}

internal sealed record RazorSearchSnapshotExtractionContext(
    IPublishedContent? Content,
    string? Culture,
    string? Segment);
