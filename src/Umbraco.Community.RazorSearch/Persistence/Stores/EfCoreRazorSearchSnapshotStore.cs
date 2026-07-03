using Microsoft.EntityFrameworkCore;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Persistence.EFCore.Scoping;
using Umbraco.Community.RazorSearch.Persistence.Models;

namespace Umbraco.Community.RazorSearch.Persistence.Stores;

public sealed class EfCoreRazorSearchSnapshotStore(IEFCoreScopeProvider<RazorSearchDbContext> scopeProvider) : IRazorSearchSnapshotStore
{
    public async Task<RazorSearchSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<RazorSearchDbContext> scope = scopeProvider.CreateScope(RepositoryCacheMode.Unspecified, true);

        RazorSearchSnapshot? snapshot = await scope.ExecuteWithContextAsync(async dbContext =>
            (await dbContext.RazorSearchSnapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken))
            ?.ToModel());

        scope.Complete();
        return snapshot;
    }

    public async Task<IReadOnlyCollection<RazorSearchSnapshot>> GetManyAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        Guid[] normalizedIds = ids.Distinct().ToArray();
        if (normalizedIds.Length == 0)
        {
            return [];
        }

        using IEfCoreScope<RazorSearchDbContext> scope = scopeProvider.CreateScope(RepositoryCacheMode.Unspecified, true);

        IReadOnlyCollection<RazorSearchSnapshot> snapshots = await scope.ExecuteWithContextAsync(async dbContext =>
            await dbContext.RazorSearchSnapshots
                .AsNoTracking()
                .Where(x => normalizedIds.Contains(x.Id))
                .OrderBy(x => x.UpdatedAtUtc)
                .Select(x => x.ToModel())
                .ToArrayAsync(cancellationToken));

        scope.Complete();
        return snapshots;
    }

    public Task<IReadOnlyCollection<RazorSearchSnapshot>> GetByContentKeyAsync(Guid contentKey, CancellationToken cancellationToken = default)
        => GetByContentKeysAsync([contentKey], cancellationToken);

    public async Task<IReadOnlyCollection<RazorSearchSnapshot>> GetByContentKeysAsync(IEnumerable<Guid> contentKeys, CancellationToken cancellationToken = default)
    {
        Guid[] normalizedKeys = contentKeys.Distinct().ToArray();
        if (normalizedKeys.Length == 0)
        {
            return [];
        }

        using IEfCoreScope<RazorSearchDbContext> scope = scopeProvider.CreateScope(RepositoryCacheMode.Unspecified, true);

        IReadOnlyCollection<RazorSearchSnapshot> snapshots = await scope.ExecuteWithContextAsync(async dbContext =>
            await dbContext.RazorSearchSnapshots
                .AsNoTracking()
                .Where(x => normalizedKeys.Contains(x.ContentKey))
                .OrderBy(x => x.ContentKey)
                .ThenBy(x => x.Route)
                .ThenBy(x => x.Culture)
                .ThenBy(x => x.Segment)
                .Select(x => x.ToModel())
                .ToArrayAsync(cancellationToken));

        scope.Complete();
        return snapshots;
    }

    public async Task<IReadOnlyCollection<RazorSearchSnapshot>> SearchAsync(IEnumerable<string> terms, CancellationToken cancellationToken = default)
    {
        string[] normalizedTerms = terms
            .Select(x => x.Trim())
            .Where(x => string.IsNullOrWhiteSpace(x) is false)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedTerms.Length == 0)
        {
            return [];
        }

        using IEfCoreScope<RazorSearchDbContext> scope = scopeProvider.CreateScope(RepositoryCacheMode.Unspecified, true);

        IReadOnlyCollection<RazorSearchSnapshot> snapshots = await scope.ExecuteWithContextAsync(async dbContext =>
        {
            IQueryable<RazorSearchSnapshotEntity> query = dbContext.RazorSearchSnapshots.AsNoTracking();
            query = query.Where(x => x.RenderStatus == RazorSearchSnapshotStatuses.Success);

            foreach (string term in normalizedTerms)
            {
                string currentTerm = term;
                query = query.Where(x =>
                    x.Snapshot.Contains(currentTerm)
                    || (x.TitleText != null && x.TitleText.Contains(currentTerm))
                    || (x.SummaryText != null && x.SummaryText.Contains(currentTerm))
                    || (x.HeadingText != null && x.HeadingText.Contains(currentTerm))
                    || (x.BodyText != null && x.BodyText.Contains(currentTerm))
                    || (x.FinalUrl != null && x.FinalUrl.Contains(currentTerm))
                    || x.Route.Contains(currentTerm));
            }

            return await query
                .OrderByDescending(x => x.UpdatedAtUtc)
                .Select(x => x.ToModel())
                .ToArrayAsync(cancellationToken);
        });

        scope.Complete();
        return snapshots;
    }

    public async Task<RazorSearchSnapshot> UpsertAsync(RazorSearchSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<RazorSearchSnapshot> snapshots = await UpsertManyAsync([snapshot], cancellationToken);
        return snapshots.Single();
    }

    public async Task<IReadOnlyCollection<RazorSearchSnapshot>> UpsertManyAsync(IEnumerable<RazorSearchSnapshot> snapshots, CancellationToken cancellationToken = default)
    {
        RazorSearchSnapshot[] normalizedSnapshots = snapshots.ToArray();
        if (normalizedSnapshots.Length == 0)
        {
            return [];
        }

        using IEfCoreScope<RazorSearchDbContext> scope = scopeProvider.CreateScope();

        IReadOnlyCollection<RazorSearchSnapshot> upsertedSnapshots = await scope.ExecuteWithContextAsync(async dbContext =>
        {
            var persistedSnapshots = new List<RazorSearchSnapshot>(normalizedSnapshots.Length);

            foreach (RazorSearchSnapshot snapshot in normalizedSnapshots)
            {
                RazorSearchSnapshotEntity? existingEntity = await dbContext.RazorSearchSnapshots.FirstOrDefaultAsync(
                    x => x.ContentKey == snapshot.ContentKey
                        && x.Route == snapshot.Route
                        && x.Culture == NormalizeNullable(snapshot.Culture)
                        && x.Segment == NormalizeNullable(snapshot.Segment)
                        && x.Renderer == snapshot.Renderer,
                    cancellationToken);

                DateTimeOffset createdAtUtc = existingEntity?.CreatedAtUtc
                    ?? (snapshot.CreatedAtUtc == default ? snapshot.UpdatedAtUtc : snapshot.CreatedAtUtc);

                RazorSearchSnapshotEntity entity = existingEntity ?? new RazorSearchSnapshotEntity
                {
                    Id = snapshot.Id == Guid.Empty ? Guid.NewGuid() : snapshot.Id,
                    CreatedAtUtc = createdAtUtc == default ? DateTimeOffset.UtcNow : createdAtUtc,
                };

                entity.ContentKey = snapshot.ContentKey;
                entity.Route = snapshot.Route;
                entity.Culture = NormalizeNullable(snapshot.Culture);
                entity.Segment = NormalizeNullable(snapshot.Segment);
                entity.Renderer = snapshot.Renderer;
                entity.Snapshot = snapshot.Snapshot;
                entity.Checksum = snapshot.Checksum;
                entity.FinalUrl = snapshot.FinalUrl;
                entity.TitleText = snapshot.TitleText;
                entity.SummaryText = snapshot.SummaryText;
                entity.HeadingText = snapshot.HeadingText;
                entity.BodyText = snapshot.BodyText;
                entity.ContentHash = snapshot.ContentHash;
                entity.RenderStatus = string.IsNullOrWhiteSpace(snapshot.RenderStatus)
                    ? RazorSearchSnapshotStatuses.Success
                    : snapshot.RenderStatus;
                entity.LastRenderError = snapshot.LastRenderError;
                entity.RenderedAtUtc = snapshot.RenderedAtUtc;
                entity.UpdatedAtUtc = snapshot.UpdatedAtUtc == default ? DateTimeOffset.UtcNow : snapshot.UpdatedAtUtc;

                if (existingEntity is null)
                {
                    if (entity.CreatedAtUtc == default)
                    {
                        entity.CreatedAtUtc = entity.UpdatedAtUtc;
                    }

                    await dbContext.RazorSearchSnapshots.AddAsync(entity, cancellationToken);
                }

                persistedSnapshots.Add(entity.ToModel());
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return persistedSnapshots;
        });

        scope.Complete();
        return upsertedSnapshots;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<RazorSearchDbContext> scope = scopeProvider.CreateScope();

        bool deleted = await scope.ExecuteWithContextAsync(async dbContext =>
        {
            RazorSearchSnapshotEntity? entity = await dbContext.RazorSearchSnapshots.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity is null)
            {
                return false;
            }

            dbContext.RazorSearchSnapshots.Remove(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        });

        if (deleted)
        {
            scope.Complete();
        }

        return deleted;
    }

    public async Task<int> DeleteByContentKeyAsync(Guid contentKey, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<RazorSearchDbContext> scope = scopeProvider.CreateScope();

        int deletedCount = await scope.ExecuteWithContextAsync(async dbContext =>
        {
            RazorSearchSnapshotEntity[] entities = await dbContext.RazorSearchSnapshots
                .Where(x => x.ContentKey == contentKey)
                .ToArrayAsync(cancellationToken);

            if (entities.Length == 0)
            {
                return 0;
            }

            dbContext.RazorSearchSnapshots.RemoveRange(entities);
            await dbContext.SaveChangesAsync(cancellationToken);
            return entities.Length;
        });

        if (deletedCount > 0)
        {
            scope.Complete();
        }

        return deletedCount;
    }

    private static string NormalizeNullable(string? value) => value ?? string.Empty;
}

internal static class RazorSearchSnapshotStoreMappings
{
    public static RazorSearchSnapshot ToModel(this RazorSearchSnapshotEntity entity) => new()
    {
        Id = entity.Id,
        ContentKey = entity.ContentKey,
        Route = entity.Route,
        Culture = DenormalizeNullable(entity.Culture),
        Segment = DenormalizeNullable(entity.Segment),
        Renderer = entity.Renderer,
        Snapshot = entity.Snapshot,
        Checksum = entity.Checksum,
        FinalUrl = entity.FinalUrl,
        TitleText = entity.TitleText,
        SummaryText = entity.SummaryText,
        HeadingText = entity.HeadingText,
        BodyText = entity.BodyText,
        ContentHash = entity.ContentHash,
        RenderStatus = entity.RenderStatus,
        LastRenderError = entity.LastRenderError,
        RenderedAtUtc = entity.RenderedAtUtc,
        CreatedAtUtc = entity.CreatedAtUtc,
        UpdatedAtUtc = entity.UpdatedAtUtc,
    };

    private static string? DenormalizeNullable(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
