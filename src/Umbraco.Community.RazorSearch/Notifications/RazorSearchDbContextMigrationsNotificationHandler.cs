using Microsoft.EntityFrameworkCore;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.RazorSearch.Persistence;

namespace Umbraco.Community.RazorSearch.Notifications;

internal sealed class RazorSearchDbContextMigrationsNotificationHandler(IDbContextFactory<RazorSearchDbContext> dbContextFactory)
    : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    public async Task HandleAsync(UmbracoApplicationStartingNotification notification, CancellationToken cancellationToken)
    {
        await using RazorSearchDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken) is not { } pendingMigrations
            || !pendingMigrations.Any())
        {
            return;
        }

        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
