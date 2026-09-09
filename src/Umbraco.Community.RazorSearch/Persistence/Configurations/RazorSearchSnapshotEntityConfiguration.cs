using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Umbraco.Community.RazorSearch.Persistence.Models;

namespace Umbraco.Community.RazorSearch.Persistence.Configurations;

internal sealed class RazorSearchSnapshotEntityConfiguration : IEntityTypeConfiguration<RazorSearchSnapshotEntity>
{
    public void Configure(EntityTypeBuilder<RazorSearchSnapshotEntity> builder)
    {
        builder.ToTable("umbracoRazorSearchSnapshot");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Route)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(x => x.Culture)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Renderer)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Checksum)
            .HasMaxLength(128);

        builder.Property(x => x.FinalUrl)
            .HasMaxLength(2048);

        // Titles are extracted from HTML and have no artificial database length limit.

        builder.Property(x => x.ContentHash)
            .HasMaxLength(128);

        builder.Property(x => x.RenderStatus)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Snapshot)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(
                x => new
                {
                    x.ContentKey,
                    x.Culture,
                })
            .IsUnique();

        builder.HasIndex(x => x.ContentKey);

        builder.HasIndex(x => x.RenderStatus);

        builder.HasIndex(x => x.RenderedAtUtc);
    }
}
