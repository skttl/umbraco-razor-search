using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.AuthorizationStatus;
using Umbraco.Cms.Core.Sync;
using Umbraco.Extensions;
using Umbraco.Community.RazorSearch.Api;
using Umbraco.Community.RazorSearch.Api.Models;
using Umbraco.Community.RazorSearch.Services;

namespace Umbraco.Community.RazorSearch.Controllers;

[ApiController]
[Authorize(Policy = "BackOfficeAccess")]
[Route("umbraco/management/api/v1/razor-search")]
public sealed class RazorSearchManagementController(
    IRazorSearchManagementService managementService,
    IServiceProvider serviceProvider,
    IBackOfficeSecurityAccessor securityAccessor,
    IContentPermissionService permissionService,
    IServerRoleAccessor serverRoleAccessor) : ControllerBase
{
    private static readonly JsonSerializerOptions QueueStatusSerializerOptions = new(JsonSerializerDefaults.Web);

    [HttpPost("document/{id:guid}/queue")]
    [HttpPost("document/{id:guid}/rebuild")]
    [ProducesResponseType<QueueRazorSearchDocumentResponse>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<QueueRazorSearchDocumentResponse>> QueueDocument(
        Guid id,
        [FromBody] QueueRazorSearchDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsRenderServer) return RenderServerUnavailable();
        var user = securityAccessor.BackOfficeSecurity?.CurrentUser;
        if (user is null || await permissionService.AuthorizeAccessAsync(user, [id], new HashSet<string> { ActionPublish.ActionLetter }) != ContentAuthorizationStatus.Success)
            return Forbid();
        if (request.IncludeDescendants && await permissionService.AuthorizeDescendantsAccessAsync(user, id, new HashSet<string> { ActionPublish.ActionLetter }) != ContentAuthorizationStatus.Success)
            return Forbid();

        QueueRazorSearchDocumentResponse response = await managementService.QueueDocumentAsync(
            id,
            request.IncludeDescendants,
            user.Key,
            cancellationToken);

        if (!user.IsAdmin())
            response = response with { Status = response.Status with { Snapshots = response.Status.Snapshots.Select(snapshot => snapshot with { SnapshotHtml = null }).ToArray() } };
        return Accepted(response);
    }

    [HttpPost("published/rebuild")]
    [ProducesResponseType<QueueRazorSearchPublishedContentResponse>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<QueueRazorSearchPublishedContentResponse>> QueuePublishedContent(
        [FromBody] QueueRazorSearchPublishedContentRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsRenderServer) return RenderServerUnavailable();
        var user = securityAccessor.BackOfficeSecurity?.CurrentUser;
        if (user?.IsAdmin() is not true) return Forbid();

        QueueRazorSearchPublishedContentResponse response = await managementService.QueuePublishedContentAsync(
            user.Key,
            cancellationToken);

        return Accepted(response);
    }

    [HttpGet("document/{id:guid}/status")]
    [ProducesResponseType<RazorSearchDocumentStatusResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RazorSearchDocumentStatusResponse>> GetDocumentStatus(Guid id, CancellationToken cancellationToken)
    {
        if (!IsRenderServer) return RenderServerUnavailable();
        var user = securityAccessor.BackOfficeSecurity?.CurrentUser;
        if (user is null || await permissionService.AuthorizeAccessAsync(user, [id], new HashSet<string> { ActionBrowse.ActionLetter }) != ContentAuthorizationStatus.Success)
            return Forbid();
        RazorSearchDocumentStatusResponse response = await managementService.GetDocumentStatusAsync(id, cancellationToken);
        if (!user.IsAdmin())
            response = response with { Snapshots = response.Snapshots.Select(snapshot => snapshot with { SnapshotHtml = null }).ToArray() };
        return Ok(response);
    }

    [HttpGet("queue/status")]
    [ProducesResponseType<RazorSearchQueueStatusResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RazorSearchQueueStatusResponse>> GetQueueStatus(CancellationToken cancellationToken)
    {
        if (!IsRenderServer) return RenderServerUnavailable();
        if (securityAccessor.BackOfficeSecurity?.CurrentUser?.IsAdmin() is not true) return Forbid();
        RazorSearchQueueStatusResponse response = await managementService.GetQueueStatusAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("queue/batch")]
    [ProducesResponseType<RazorSearchQueueBatchDetailsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RazorSearchQueueBatchDetailsResponse>> GetQueueBatchDetails(CancellationToken cancellationToken)
    {
        if (!IsRenderServer) return RenderServerUnavailable();
        if (securityAccessor.BackOfficeSecurity?.CurrentUser?.IsAdmin() is not true) return Forbid();
        RazorSearchQueueBatchDetailsResponse response = await managementService.GetQueueBatchDetailsAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("queue/stream")]
    public async Task StreamQueueStatus(CancellationToken cancellationToken)
    {
        if (!IsRenderServer) { Response.StatusCode = StatusCodes.Status503ServiceUnavailable; return; }
        if (securityAccessor.BackOfficeSecurity?.CurrentUser?.IsAdmin() is not true) { Response.StatusCode = StatusCodes.Status403Forbidden; return; }

        IRazorSearchQueueActivityNotifier queueActivityNotifier =
            serviceProvider.GetRequiredService<IRazorSearchQueueActivityNotifier>();

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Append("X-Accel-Buffering", "no");

        await WriteQueueStatusEventAsync(cancellationToken);

        await using IRazorSearchQueueActivitySubscription subscription = queueActivityNotifier.Subscribe(cancellationToken);

        while (await subscription.Reader.WaitToReadAsync(cancellationToken))
        {
            while (subscription.Reader.TryRead(out _))
            {
            }

            await WriteQueueStatusEventAsync(cancellationToken);
        }
    }

    private bool IsRenderServer => serverRoleAccessor.CurrentServerRole is ServerRole.Single or ServerRole.SchedulingPublisher;

    private ObjectResult RenderServerUnavailable() => Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "RazorSearch operations are available on the dedicated backoffice server.");

    private async Task WriteQueueStatusEventAsync(CancellationToken cancellationToken)
    {
        RazorSearchQueueStatusResponse response = await managementService.GetQueueStatusAsync(cancellationToken);
        string payload = JsonSerializer.Serialize(response, QueueStatusSerializerOptions);
        await Response.WriteAsync($"event: queue-status\ndata: {payload}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
