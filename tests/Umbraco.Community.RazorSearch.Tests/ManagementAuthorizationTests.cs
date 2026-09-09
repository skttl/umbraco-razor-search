using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.AuthorizationStatus;
using Umbraco.Cms.Core.Sync;
using Umbraco.Community.RazorSearch.Api;
using Umbraco.Community.RazorSearch.Api.Models;
using Umbraco.Community.RazorSearch.Controllers;

namespace Umbraco.Community.RazorSearch.Tests;

public class ManagementAuthorizationTests
{
    [Theory]
    [InlineData(ContentAuthorizationStatus.UnauthorizedMissingPathAccess)]
    [InlineData(ContentAuthorizationStatus.UnauthorizedMissingPermissionAccess)]
    [InlineData(ContentAuthorizationStatus.NotFound)]
    public async Task RebuildRequiresPublishPermissionWithinStartNodes(ContentAuthorizationStatus denied)
    {
        var fixture = new Fixture();
        fixture.Permissions.Setup(x => x.AuthorizeAccessAsync(fixture.User, It.IsAny<IEnumerable<Guid>>(), It.Is<ISet<string>>(p => p.Contains("Umb.Document.Publish"))))
            .ReturnsAsync(denied);

        var result = await fixture.Controller.QueueDocument(Guid.NewGuid(), new(), default);

        Assert.IsType<ForbidResult>(result.Result);
        fixture.Management.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeniedDescendantRejectsWholeRebuild()
    {
        var fixture = new Fixture();
        fixture.AllowDocument();
        fixture.Permissions.Setup(x => x.AuthorizeDescendantsAccessAsync(fixture.User, It.IsAny<Guid>(), It.IsAny<ISet<string>>()))
            .ReturnsAsync(ContentAuthorizationStatus.UnauthorizedMissingPermissionAccess);

        var result = await fixture.Controller.QueueDocument(Guid.NewGuid(), new() { IncludeDescendants = true }, default);

        Assert.IsType<ForbidResult>(result.Result);
        fixture.Management.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AuthorizedDocumentQueuesWithActorAndRedactsHtml()
    {
        var fixture = new Fixture();
        fixture.AllowDocument();
        var id = Guid.NewGuid();
        fixture.Management.Setup(x => x.QueueDocumentAsync(id, false, fixture.User.Key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueRazorSearchDocumentResponse { Status = StatusWithHtml() });

        var result = await fixture.Controller.QueueDocument(id, new(), default);

        var response = Assert.IsType<QueueRazorSearchDocumentResponse>(Assert.IsType<AcceptedResult>(result.Result).Value);
        Assert.Null(Assert.Single(response.Status.Snapshots).SnapshotHtml);
        fixture.Permissions.Verify(x => x.AuthorizeAccessAsync(fixture.User, It.IsAny<IEnumerable<Guid>>(), It.Is<ISet<string>>(p => p.SetEquals(new[] { "Umb.Document.Publish" }))), Times.Once);
        fixture.Management.Verify(x => x.QueueDocumentAsync(id, false, fixture.User.Key, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EditorStatusExposesTextButNeverFullHtml()
    {
        var fixture = new Fixture();
        fixture.AllowDocument();
        fixture.Management.Setup(x => x.GetDocumentStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(StatusWithHtml());

        var result = await fixture.Controller.GetDocumentStatus(Guid.NewGuid(), default);

        var status = Assert.IsType<RazorSearchDocumentStatusResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var snapshot = Assert.Single(status.Snapshots);
        Assert.Null(snapshot.SnapshotHtml);
        Assert.Equal("Visible title", snapshot.TitleText);
        fixture.Permissions.Verify(x => x.AuthorizeAccessAsync(fixture.User, It.IsAny<IEnumerable<Guid>>(), It.Is<ISet<string>>(p => p.SetEquals(new[] { "Umb.Document.Read" }))), Times.Once);
    }

    [Fact]
    public async Task AdministratorCanReadHtmlAndQueueGlobalRebuild()
    {
        var fixture = new Fixture(admin: true);
        fixture.AllowDocument();
        fixture.Management.Setup(x => x.GetDocumentStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(StatusWithHtml());
        fixture.Management.Setup(x => x.QueuePublishedContentAsync(fixture.User.Key, It.IsAny<CancellationToken>())).ReturnsAsync(new QueueRazorSearchPublishedContentResponse());

        var result = await fixture.Controller.GetDocumentStatus(Guid.NewGuid(), default);
        var status = Assert.IsType<RazorSearchDocumentStatusResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("<html>private raw snapshot</html>", Assert.Single(status.Snapshots).SnapshotHtml);
        Assert.IsType<AcceptedResult>((await fixture.Controller.QueuePublishedContent(new(), default)).Result);
    }

    [Fact]
    public async Task EditorsCannotAccessGlobalRebuildOrOtherDocumentsQueue()
    {
        var fixture = new Fixture();
        Assert.IsType<ForbidResult>((await fixture.Controller.QueuePublishedContent(new(), default)).Result);
        Assert.IsType<ForbidResult>((await fixture.Controller.GetQueueStatus(default)).Result);
        Assert.IsType<ForbidResult>((await fixture.Controller.GetQueueBatchDetails(default)).Result);
        await fixture.Controller.StreamQueueStatus(default);
        Assert.Equal(StatusCodes.Status403Forbidden, fixture.Controller.Response.StatusCode);
        fixture.Management.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SubscriberRejectsOperationsWithoutQueuingWork()
    {
        var fixture = new Fixture(admin: true, role: ServerRole.Subscriber);
        var result = await fixture.Controller.QueueDocument(Guid.NewGuid(), new(), default);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<ObjectResult>((await fixture.Controller.GetQueueStatus(default)).Result).StatusCode);
        fixture.Management.VerifyNoOtherCalls();
    }

    private static RazorSearchDocumentStatusResponse StatusWithHtml() => new()
    {
        Snapshots = [new RazorSearchDocumentSnapshotResponse { SnapshotHtml = "<html>private raw snapshot</html>", TitleText = "Visible title" }],
    };

    private sealed class Fixture
    {
        public Mock<IRazorSearchManagementService> Management { get; } = new(MockBehavior.Strict);
        public Mock<IContentPermissionService> Permissions { get; } = new();
        public IUser User { get; }
        public RazorSearchManagementController Controller { get; }

        public Fixture(bool admin = false, ServerRole role = ServerRole.Single)
        {
            var user = new Mock<IUser>();
            user.SetupGet(x => x.Key).Returns(Guid.NewGuid());
            user.SetupGet(x => x.Groups).Returns(admin ? [Mock.Of<IReadOnlyUserGroup>(g => g.Alias == "admin")] : []);
            User = user.Object;
            var security = Mock.Of<IBackOfficeSecurity>(x => x.CurrentUser == User);
            Controller = new(Management.Object, Mock.Of<IServiceProvider>(),
                Mock.Of<IBackOfficeSecurityAccessor>(x => x.BackOfficeSecurity == security), Permissions.Object,
                Mock.Of<IServerRoleAccessor>(x => x.CurrentServerRole == role));
            Controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        }

        public void AllowDocument() => Permissions.Setup(x => x.AuthorizeAccessAsync(User, It.IsAny<IEnumerable<Guid>>(), It.IsAny<ISet<string>>()))
            .ReturnsAsync(ContentAuthorizationStatus.Success);
    }
}
