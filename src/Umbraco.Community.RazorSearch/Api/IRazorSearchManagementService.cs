using Umbraco.Community.RazorSearch.Api.Models;

namespace Umbraco.Community.RazorSearch.Api;

public interface IRazorSearchManagementService
{
    Task<QueueRazorSearchDocumentResponse> QueueDocumentAsync(Guid documentId, bool includeDescendants, Guid userKey, CancellationToken cancellationToken);

    Task<QueueRazorSearchPublishedContentResponse> QueuePublishedContentAsync(Guid userKey, CancellationToken cancellationToken);

    Task<RazorSearchDocumentStatusResponse> GetDocumentStatusAsync(Guid documentId, CancellationToken cancellationToken);

    Task<RazorSearchQueueStatusResponse> GetQueueStatusAsync(CancellationToken cancellationToken);

    Task<RazorSearchQueueBatchDetailsResponse> GetQueueBatchDetailsAsync(CancellationToken cancellationToken);
}
