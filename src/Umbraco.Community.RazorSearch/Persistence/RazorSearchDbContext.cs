using Microsoft.EntityFrameworkCore;
using Umbraco.Community.RazorSearch.Persistence.Configurations;
using Umbraco.Community.RazorSearch.Persistence.Models;

namespace Umbraco.Community.RazorSearch.Persistence;

public sealed class RazorSearchDbContext(DbContextOptions<RazorSearchDbContext> options) : DbContext(options)
{
    internal DbSet<RazorSearchSnapshotEntity> RazorSearchSnapshots => Set<RazorSearchSnapshotEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new RazorSearchSnapshotEntityConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
