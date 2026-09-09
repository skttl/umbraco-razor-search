using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.RazorSearch.Services;

namespace Umbraco.Community.RazorSearch.Tests;

public sealed class RebuildPaginationTests
{
    [Fact]
    public void Rebuild_enumerates_all_pages_beyond_five_thousand_documents()
    {
        const int descendantCount = 5137;
        var root = new Mock<IContent>();
        root.SetupGet(x => x.Id).Returns(1);
        var descendants = Enumerable.Range(0, descendantCount).Select(i =>
        {
            var content = new Mock<IContent>();
            content.SetupGet(x => x.Id).Returns(i + 2);
            return content.Object;
        }).ToArray();
        var service = new Mock<IContentService>();
        long total = descendantCount;
        service.Setup(x => x.GetPagedDescendants(1, It.IsAny<long>(), 128, out total, null,
            It.Is<Ordering>(o => o.OrderBy == "id")))
            .Returns((int id, long page, int size, long count, IQuery<IContent>? filter, Ordering order) =>
                descendants.Skip((int)page * size).Take(size));

        var result = RazorSearchRebuildQueue.Enumerate(service.Object, [root.Object], true).ToArray();

        Assert.Equal(descendantCount + 1, result.Length);
        Assert.Equal(Enumerable.Range(1, descendantCount + 1), result.Select(x => x.Id));
        service.Verify(x => x.GetPagedDescendants(1, 40, 128, out total, null, It.IsAny<Ordering>()), Times.Once);
    }

    [Fact]
    public void Document_only_rebuild_does_not_enumerate_descendants()
    {
        var service = new Mock<IContentService>(MockBehavior.Strict);
        var root = new Mock<IContent>().Object;
        Assert.Same(root, Assert.Single(RazorSearchRebuildQueue.Enumerate(service.Object, [root], false)));
        service.VerifyNoOtherCalls();
    }
}
