using System.Text;
using Microsoft.Extensions.Configuration;
using Umbraco.Community.RazorSearch.Configuration;
using Umbraco.Community.RazorSearch.Services;

namespace Umbraco.Community.RazorSearch.Tests;

public sealed class RazorSearchConfigurationTests
{
    [Fact]
    public void ConfiguredSourcesReplaceDefaultsAndEmptyArraysDisableGroups()
    {
        var options = Bind("""
        {"Umbraco":{"Community":{"RazorSearch":{"SnapshotExtraction":{
          "TitleSources":[{"Type":"property","Alias":"seoTitle"},{"Type":"selector","Selector":"title"}],
          "BodySources":[{"Type":"selector","Selector":"main"}],"HeadingSources":[]}}}}}
        """);
        Assert.Equal(new[] { "property", "selector" }, options.SnapshotExtraction.TitleSources.Select(x => x.Type));
        Assert.Equal("seoTitle", options.SnapshotExtraction.TitleSources[0].Alias);
        Assert.Equal("main", Assert.Single(options.SnapshotExtraction.BodySources).Selector);
        Assert.Empty(options.SnapshotExtraction.HeadingSources);
        Assert.Equal("meta[name='description']", Assert.Single(options.SnapshotExtraction.SummarySources).Selector);
        Assert.True(new RazorSearchOptionsValidator().Validate(null, options).Succeeded);
    }

    [Fact]
    public void MissingNewSectionUsesDefaultsAndIgnoresLegacyRoot()
    {
        var options = Bind("""{"RazorSearch":{"DefaultRenderer":"legacy","SnapshotExtraction":{"BodySources":[]}}}""");
        Assert.Equal("http", options.DefaultRenderer);
        Assert.Equal("body", Assert.Single(options.SnapshotExtraction.BodySources).Selector);
    }

    [Theory]
    [InlineData("{\"Type\":\"unknown\",\"Selector\":\"main\"}")]
    [InlineData("{\"Type\":\"selector\",\"Selector\":\"main[\"}")]
    [InlineData("{\"Type\":\"property\"}")]
    [InlineData("{\"Type\":\"property\",\"Alias\":\"title\",\"Selector\":\"title\"}")]
    public void InvalidSourceFailsValidationInsteadOfFallingBack(string source)
    {
        var options = Bind("{\"Umbraco\":{\"Community\":{\"RazorSearch\":{\"SnapshotExtraction\":{\"BodySources\":[" + source + "]}}}}}");
        var result = new RazorSearchOptionsValidator().Validate(null, options);
        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, x => x.Contains("SnapshotExtraction:BodySources:0", StringComparison.Ordinal));
        Assert.Throws<InvalidOperationException>(() => RazorSearchSnapshotTextSanitizer.ExtractSnapshotContent("<body>private navigation</body>", "/", null, extractionOptions: options.SnapshotExtraction));
    }

    [Fact]
    public void NullSourcesAreRejectedWhileEmptyArraysRemainValid()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Bind("""{"Umbraco":{"Community":{"RazorSearch":{"SnapshotExtraction":{"BodySources":null}}}}}"""));
        Assert.Contains("BodySources cannot be null", error.Message);
        var empty = Bind("""{"Umbraco":{"Community":{"RazorSearch":{"SnapshotExtraction":{"BodySources":[]}}}}}""");
        Assert.Empty(empty.SnapshotExtraction.BodySources);
    }

    [Fact]
    public void MisspelledExtractionSettingDoesNotSilentlyUseBodyDefault()
    {
        Assert.Throws<InvalidOperationException>(() => Bind("""{"Umbraco":{"Community":{"RazorSearch":{"SnapshotExtraction":{"BodySource":[]}}}}}"""));
    }

    [Fact]
    public void InternalOriginRequiresSharedTokenAndCannotContainPath()
    {
        var options = new RazorSearchOptions();
        options.HttpRenderer.RenderBaseAddress = "http://localhost:5000/path";
        var invalid = new RazorSearchOptionsValidator().Validate(null, options);
        Assert.Contains(invalid.Failures!, x => x.Contains("origin without path", StringComparison.Ordinal));
        Assert.Contains(invalid.Failures!, x => x.Contains("RenderRequestToken is required", StringComparison.Ordinal));
        options.HttpRenderer.RenderBaseAddress = "http://localhost:5000";
        options.RenderRequestToken = "test-only-shared-secret";
        Assert.True(new RazorSearchOptionsValidator().Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData(0, 3, 1000)]
    [InlineData(256, 0, 1000)]
    [InlineData(256, 11, 1000)]
    [InlineData(256, 3, -1)]
    public void InvalidQueueBoundsFailValidation(int capacity, int attempts, int retention)
    {
        var options = new RazorSearchOptions { RenderQueue = new() { Capacity = capacity, MaxAttempts = attempts, CompletedJobRetention = retention } };
        Assert.True(new RazorSearchOptionsValidator().Validate(null, options).Failed);
    }

    internal static RazorSearchOptions Bind(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        IConfiguration config = new ConfigurationBuilder().AddJsonStream(stream).Build();
        var options = new RazorSearchOptions();
        new ConfigureRazorSearchOptions(config).Configure(options);
        return options;
    }
}
