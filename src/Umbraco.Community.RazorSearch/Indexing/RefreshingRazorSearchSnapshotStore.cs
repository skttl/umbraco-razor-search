using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;
using Umbraco.Community.RazorSearch.Persistence.Models;
using Umbraco.Community.RazorSearch.Persistence.Stores;

namespace Umbraco.Community.RazorSearch.Indexing;

internal sealed class RefreshingRazorSearchSnapshotStore(
    IRazorSearchSnapshotStore innerStore,
    IContentService contentService,
    IDistributedContentIndexRefresher distributedContentIndexRefresher)
    : IRazorSearchSnapshotStore
{
    public Task<RazorSearchSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        innerStore.GetAsync(id, cancellationToken);

    public Task<IReadOnlyCollection<RazorSearchSnapshot>> GetManyAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default) =>
        innerStore.GetManyAsync(ids, cancellationToken);

    public Task<IReadOnlyCollection<RazorSearchSnapshot>> GetByContentKeyAsync(Guid contentKey, CancellationToken cancellationToken = default) =>
        innerStore.GetByContentKeyAsync(contentKey, cancellationToken);

    public Task<IReadOnlyCollection<RazorSearchSnapshot>> GetByContentKeysAsync(IEnumerable<Guid> contentKeys, CancellationToken cancellationToken = default) =>
        innerStore.GetByContentKeysAsync(contentKeys, cancellationToken);

    public Task<IReadOnlyCollection<RazorSearchSnapshot>> SearchAsync(IEnumerable<string> terms, CancellationToken cancellationToken = default) =>
        innerStore.SearchAsync(terms, cancellationToken);

    public async Task<RazorSearchSnapshot> UpsertAsync(RazorSearchSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        RazorSearchSnapshot persistedSnapshot = await innerStore.UpsertAsync(snapshot, cancellationToken);
        RefreshPublishedContentIndex([persistedSnapshot.ContentKey]);
        return persistedSnapshot;
    }

    public async Task<IReadOnlyCollection<RazorSearchSnapshot>> UpsertManyAsync(IEnumerable<RazorSearchSnapshot> snapshots, CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<RazorSearchSnapshot> persistedSnapshots = await innerStore.UpsertManyAsync(snapshots, cancellationToken);
        RefreshPublishedContentIndex(persistedSnapshots.Select(x => x.ContentKey));
        return persistedSnapshots;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RazorSearchSnapshot? snapshot = await innerStore.GetAsync(id, cancellationToken);
        if (snapshot is null)
        {
            return false;
        }

        bool deleted = await innerStore.DeleteAsync(id, cancellationToken);
        if (deleted)
        {
            RefreshPublishedContentIndex([snapshot.ContentKey]);
        }

        return deleted;
    }

    public async Task<int> DeleteByContentKeyAsync(Guid contentKey, CancellationToken cancellationToken = default)
    {
        int deletedCount = await innerStore.DeleteByContentKeyAsync(contentKey, cancellationToken);
        if (deletedCount > 0)
        {
            RefreshPublishedContentIndex([contentKey]);
        }

        return deletedCount;
    }

    private void RefreshPublishedContentIndex(IEnumerable<Guid> contentKeys)
    {
        IContent[] contentItems = contentKeys
            .Distinct()
            .Select(contentService.GetById)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToArray();

        if (contentItems.Length == 0)
        {
            return;
        }

        distributedContentIndexRefresher.RefreshContent(contentItems, ContentState.Published);
    }
}
