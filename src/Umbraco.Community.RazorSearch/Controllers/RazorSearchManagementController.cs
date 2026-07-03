using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Community.RazorSearch.Api;
using Umbraco.Community.RazorSearch.Api.Models;

namespace Umbraco.Community.RazorSearch.Controllers;

[ApiController]
[Authorize(Policy = "BackOfficeAccess")]
[Route("umbraco/management/api/v1/razor-search")]
public sealed class RazorSearchManagementController(IRazorSearchManagementService managementService) : ControllerBase
{
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
}
