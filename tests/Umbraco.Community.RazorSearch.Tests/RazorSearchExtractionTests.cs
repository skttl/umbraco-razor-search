using Moq;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Community.RazorSearch.Configuration;
using Umbraco.Community.RazorSearch.Services;

namespace Umbraco.Community.RazorSearch.Tests;

public sealed class RazorSearchExtractionTests
{
    [Theory]
    [InlineData("hel<strong>lo</strong>", "hello")]
    [InlineData("<div title='a > b'>Visible</div>", "Visible")]
    [InlineData("<p>One</p><p>Two<br>Three</p>", "One Two Three")]
    [InlineData("<p>&amp;lt; &amp; &nbsp; end</p>", "&lt; & end")]
    [InlineData("<script>ignored</script><style>ignored</style><noscript>ignored</noscript><template>ignored</template><p>visible</p>", "visible")]
    public void PlainTextUsesHtmlSemantics(string html, string expected)
        => Assert.Equal(expected, RazorSearchSnapshotTextSanitizer.ExtractPlainText(html));

    [Fact]
    public void CssSelectionAndRemovalRestrictBodyWithoutGlobalOptionsState()
    {
        var extraction = new RazorSearchSnapshotExtractionOptions
        {
            BodySources = [new() { Selector = "main > article:not(.excluded)" }],
            RemoveSelectors = [".private", "aside"],
        };
        var unrelated = new RazorSearchSnapshotExtractionOptions { BodySources = [] };
        const string html = "<nav>navigation</nav><main><article>Hello <b>world</b><span class='private'>secret</span><aside>sidebar</aside></article><article class='excluded'>other</article></main>";
        var result = RazorSearchSnapshotTextSanitizer.ExtractSnapshotContent(html, "/", null, extractionOptions: extraction);
        Assert.Equal("Hello world", result.BodyText);
        Assert.Empty(RazorSearchSnapshotTextSanitizer.ExtractSnapshotContent(html, "/", null, extractionOptions: unrelated).BodyText);
        Assert.Equal("Hello world", RazorSearchSnapshotTextSanitizer.ExtractSnapshotContent(html, "/", null, extractionOptions: extraction).BodyText);
    }

    [Fact]
    public void FirstNonemptyPropertyWinsBeforeHtmlTitleForTheRequestedCulture()
    {
        var property = new Mock<IPublishedProperty>();
        property.Setup(x => x.GetValue("da-DK", null)).Returns("SEO <b>titel</b>");
        var content = new Mock<IPublishedContent>();
        content.Setup(x => x.GetProperty("seoTitle")).Returns(property.Object);
        var extraction = new RazorSearchSnapshotExtractionOptions
        {
            TitleSources = [new() { Type = "property", Alias = "missing" }, new() { Type = "property", Alias = "seoTitle" }, new() { Selector = "title" }],
        };
        var result = RazorSearchSnapshotTextSanitizer.ExtractSnapshotContent("<title>HTML title</title>", "/", null, new(content.Object, "da-DK"), extraction);
        Assert.Equal("SEO titel", result.TitleText);
        property.Verify(x => x.GetValue("da-DK", null), Times.Once);
    }

    [Fact]
    public void EmptyTitleAndBodyArraysDoNotFallBackToWholePage()
    {
        var extraction = new RazorSearchSnapshotExtractionOptions { TitleSources = [], BodySources = [], HeadingSources = [], SummarySources = [] };
        var result = RazorSearchSnapshotTextSanitizer.ExtractSnapshotContent("<title>title</title><body><h1>heading</h1>navigation</body>", "/", null, extractionOptions: extraction);
        Assert.Empty(result.TitleText);
        Assert.Empty(result.BodyText);
        Assert.Empty(result.CombinedText);
    }
}
