using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.RazorSearch.Notifications;
using Umbraco.Community.RazorSearch.Persistence;
using Umbraco.Community.RazorSearch.Persistence.Stores;
using Umbraco.Extensions;

namespace Umbraco.Community.RazorSearch.Composers;

public sealed class RazorSearchPersistenceComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddUmbracoDbContext<RazorSearchDbContext>((serviceProvider, optionsBuilder, _, _) =>
            optionsBuilder.UseUmbracoDatabaseProvider(serviceProvider), shareUmbracoConnection: true);

        builder.Services.TryAddScoped<IRazorSearchSnapshotStore, EfCoreRazorSearchSnapshotStore>();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<INotificationAsyncHandler<UmbracoApplicationStartingNotification>, RazorSearchDbContextMigrationsNotificationHandler>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<INotificationAsyncHandler<ContentPublishedNotification>, RazorSearchContentLifecycleNotificationHandler>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<INotificationAsyncHandler<ContentUnpublishedNotification>, RazorSearchContentLifecycleNotificationHandler>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<INotificationAsyncHandler<ContentDeletedNotification>, RazorSearchContentLifecycleNotificationHandler>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<INotificationAsyncHandler<ContentMovedNotification>, RazorSearchContentLifecycleNotificationHandler>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<INotificationAsyncHandler<ContentMovedToRecycleBinNotification>, RazorSearchContentLifecycleNotificationHandler>());
    }
}
