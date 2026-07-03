using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Umbraco.Community.RazorSearch.Api;
using Umbraco.Community.RazorSearch.Api.Models;
using Umbraco.Community.RazorSearch.Services;

namespace Umbraco.Community.RazorSearch.Controllers;

[ApiController]
[Authorize(Policy = "BackOfficeAccess")]
[Route("umbraco/management/api/v1/razor-search")]
public sealed class RazorSearchManagementController(
    IRazorSearchManagementService managementService,
    IServiceProvider serviceProvider) : ControllerBase
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
        QueueRazorSearchDocumentResponse response = await managementService.QueueDocumentAsync(
            id,
            request.IncludeDescendants,
            request.MaxDocuments,
            cancellationToken);

        return Accepted(response);
    }

    [HttpPost("published/rebuild")]
    [ProducesResponseType<QueueRazorSearchPublishedContentResponse>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<QueueRazorSearchPublishedContentResponse>> QueuePublishedContent(
        [FromBody] QueueRazorSearchPublishedContentRequest request,
        CancellationToken cancellationToken)
    {
        QueueRazorSearchPublishedContentResponse response = await managementService.QueuePublishedContentAsync(
            request.MaxDocuments,
            cancellationToken);

        return Accepted(response);
    }

    [HttpGet("document/{id:guid}/status")]
    [ProducesResponseType<RazorSearchDocumentStatusResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RazorSearchDocumentStatusResponse>> GetDocumentStatus(Guid id, CancellationToken cancellationToken)
    {
        RazorSearchDocumentStatusResponse response = await managementService.GetDocumentStatusAsync(id, cancellationToken);
        return Ok(response);
    }

    [HttpGet("queue/status")]
    [ProducesResponseType<RazorSearchQueueStatusResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RazorSearchQueueStatusResponse>> GetQueueStatus(CancellationToken cancellationToken)
    {
        RazorSearchQueueStatusResponse response = await managementService.GetQueueStatusAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("queue/batch")]
    [ProducesResponseType<RazorSearchQueueBatchDetailsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RazorSearchQueueBatchDetailsResponse>> GetQueueBatchDetails(CancellationToken cancellationToken)
    {
        RazorSearchQueueBatchDetailsResponse response = await managementService.GetQueueBatchDetailsAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("queue/stream")]
    public async Task StreamQueueStatus(CancellationToken cancellationToken)
    {
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

    private async Task WriteQueueStatusEventAsync(CancellationToken cancellationToken)
    {
        RazorSearchQueueStatusResponse response = await managementService.GetQueueStatusAsync(cancellationToken);
        string payload = JsonSerializer.Serialize(response, QueueStatusSerializerOptions);
        await Response.WriteAsync($"event: queue-status\ndata: {payload}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
