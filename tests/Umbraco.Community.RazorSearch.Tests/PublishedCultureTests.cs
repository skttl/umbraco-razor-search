using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Community.RazorSearch.Services;

namespace Umbraco.Community.RazorSearch.Tests;

public sealed class PublishedCultureTests
{
    [Fact]
    public void Invariant_document_with_empty_culture_dictionary_key_produces_null_culture()
    {
        var type = new Mock<IPublishedContentType>();
        type.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
        var content = new Mock<IPublishedContent>();
        content.SetupGet(x => x.ContentType).Returns(type.Object);
        content.SetupGet(x => x.Cultures).Returns(new Dictionary<string, PublishedCultureInfo> { [""] = null! });
        Assert.Null(Assert.Single(RazorSearchPublishedCultures.Get(content.Object)));
    }

    [Fact]
    public void Variant_document_produces_only_named_published_cultures()
    {
        var type = new Mock<IPublishedContentType>();
        type.SetupGet(x => x.Variations).Returns(ContentVariation.Culture);
        var content = new Mock<IPublishedContent>();
        content.SetupGet(x => x.ContentType).Returns(type.Object);
        content.SetupGet(x => x.Cultures).Returns(new Dictionary<string, PublishedCultureInfo> { ["en-US"] = null!, ["da-DK"] = null!, [""] = null! });
        Assert.Equal(new[] { "en-US", "da-DK" }, RazorSearchPublishedCultures.Get(content.Object));
    }
}
