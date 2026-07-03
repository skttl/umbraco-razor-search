using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Umbraco.Community.RazorSearch.Persistence.Migrations;

[DbContext(typeof(RazorSearchDbContext))]
internal sealed class RazorSearchDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.0");

        modelBuilder.Entity("Umbraco.Community.RazorSearch.Persistence.Models.RazorSearchSnapshotEntity", entity =>
        {
            entity.Property<Guid>("Id");
            entity.Property<string?>("Checksum")
                .HasMaxLength(128);
            entity.Property<string?>("ContentHash")
                .HasMaxLength(128);
            entity.Property<Guid>("ContentKey");
            entity.Property<string>("Culture")
                .IsRequired()
                .HasMaxLength(32);
            entity.Property<string?>("FinalUrl")
                .HasMaxLength(2048);
            entity.Property<string?>("BodyText");
            entity.Property<string?>("HeadingText");
            entity.Property<string?>("LastRenderError");
            entity.Property<DateTimeOffset?>("RenderedAtUtc");
            entity.Property<string>("RenderStatus")
                .IsRequired()
                .HasMaxLength(32);
            entity.Property<DateTimeOffset>("CreatedAtUtc");
            entity.Property<string>("Renderer")
                .IsRequired()
                .HasMaxLength(128);
            entity.Property<string>("Route")
                .IsRequired()
                .HasMaxLength(2048);
            entity.Property<string>("Segment")
                .IsRequired()
                .HasMaxLength(64);
            entity.Property<string>("Snapshot")
                .IsRequired();
            entity.Property<string?>("SummaryText");
            entity.Property<string?>("TitleText")
                .HasMaxLength(512);
            entity.Property<DateTimeOffset>("UpdatedAtUtc");

            entity.HasKey("Id");

            entity.HasIndex("ContentKey");

            entity.HasIndex("RenderedAtUtc");

            entity.HasIndex("RenderStatus");

            entity.HasIndex("ContentKey", "Route", "Culture", "Segment", "Renderer")
                .IsUnique();

            entity.ToTable("umbracoRazorSearchSnapshot");
        });
#pragma warning restore 612, 618
    }
}
