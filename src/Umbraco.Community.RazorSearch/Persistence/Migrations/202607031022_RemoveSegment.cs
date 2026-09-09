using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Umbraco.Community.RazorSearch.Persistence.Migrations;

[DbContext(typeof(RazorSearchDbContext))]
[Migration("202607031022_RemoveSegment")]
public sealed class RemoveSegment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (migrationBuilder.ActiveProvider?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            migrationBuilder.Sql("""
                CREATE TABLE umbracoRazorSearchSnapshot_new (
                    Id TEXT NOT NULL CONSTRAINT PK_umbracoRazorSearchSnapshot PRIMARY KEY,
                    ContentKey TEXT NOT NULL,
                    Route TEXT NOT NULL,
                    Culture TEXT NOT NULL,
                    Renderer TEXT NOT NULL,
                    Snapshot TEXT NOT NULL,
                    SnapshotHtml TEXT NULL,
                    Checksum TEXT NULL,
                    FinalUrl TEXT NULL,
                    TitleText TEXT NULL,
                    SummaryText TEXT NULL,
                    HeadingText TEXT NULL,
                    BodyText TEXT NULL,
                    ContentHash TEXT NULL,
                    RenderStatus TEXT NOT NULL DEFAULT 'Success',
                    LastRenderError TEXT NULL,
                    LastAttemptAtUtc TEXT NULL,
                    RenderedAtUtc TEXT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL
                );
                INSERT INTO umbracoRazorSearchSnapshot_new
                    (Id, ContentKey, Route, Culture, Renderer, Snapshot, SnapshotHtml,
                     Checksum, FinalUrl, TitleText, SummaryText, HeadingText, BodyText,
                     ContentHash, RenderStatus, LastRenderError, LastAttemptAtUtc,
                     RenderedAtUtc, CreatedAtUtc, UpdatedAtUtc)
                SELECT Id, ContentKey, Route, Culture, Renderer, Snapshot, SnapshotHtml,
                       Checksum, FinalUrl, TitleText, SummaryText, HeadingText, BodyText,
                       ContentHash, RenderStatus, LastRenderError, LastAttemptAtUtc,
                       RenderedAtUtc, CreatedAtUtc, UpdatedAtUtc
                FROM umbracoRazorSearchSnapshot;
                DROP TABLE umbracoRazorSearchSnapshot;
                ALTER TABLE umbracoRazorSearchSnapshot_new RENAME TO umbracoRazorSearchSnapshot;
                CREATE INDEX IX_umbracoRazorSearchSnapshot_ContentKey
                    ON umbracoRazorSearchSnapshot (ContentKey);
                CREATE INDEX IX_umbracoRazorSearchSnapshot_RenderedAtUtc
                    ON umbracoRazorSearchSnapshot (RenderedAtUtc);
                CREATE INDEX IX_umbracoRazorSearchSnapshot_RenderStatus
                    ON umbracoRazorSearchSnapshot (RenderStatus);
                CREATE UNIQUE INDEX IX_umbracoRazorSearchSnapshot_ContentKey_Culture
                    ON umbracoRazorSearchSnapshot (ContentKey, Culture);
                """);
            return;
        }

        migrationBuilder.DropIndex(
            name: "IX_umbracoRazorSearchSnapshot_ContentKey_Route_Culture_Segment_Renderer",
            table: "umbracoRazorSearchSnapshot");
        migrationBuilder.DropColumn(name: "Segment", table: "umbracoRazorSearchSnapshot");
        migrationBuilder.CreateIndex(
            name: "IX_umbracoRazorSearchSnapshot_ContentKey_Culture",
            table: "umbracoRazorSearchSnapshot",
            columns: ["ContentKey", "Culture"],
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (migrationBuilder.ActiveProvider?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            migrationBuilder.Sql("""
                CREATE TABLE umbracoRazorSearchSnapshot_new (
                    Id TEXT NOT NULL CONSTRAINT PK_umbracoRazorSearchSnapshot PRIMARY KEY,
                    ContentKey TEXT NOT NULL,
                    Route TEXT NOT NULL,
                    Culture TEXT NOT NULL,
                    Segment TEXT NOT NULL DEFAULT '',
                    Renderer TEXT NOT NULL,
                    Snapshot TEXT NOT NULL,
                    SnapshotHtml TEXT NULL,
                    Checksum TEXT NULL,
                    FinalUrl TEXT NULL,
                    TitleText TEXT NULL,
                    SummaryText TEXT NULL,
                    HeadingText TEXT NULL,
                    BodyText TEXT NULL,
                    ContentHash TEXT NULL,
                    RenderStatus TEXT NOT NULL DEFAULT 'Success',
                    LastRenderError TEXT NULL,
                    LastAttemptAtUtc TEXT NULL,
                    RenderedAtUtc TEXT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL
                );
                INSERT INTO umbracoRazorSearchSnapshot_new
                    (Id, ContentKey, Route, Culture, Segment, Renderer, Snapshot, SnapshotHtml,
                     Checksum, FinalUrl, TitleText, SummaryText, HeadingText, BodyText,
                     ContentHash, RenderStatus, LastRenderError, LastAttemptAtUtc,
                     RenderedAtUtc, CreatedAtUtc, UpdatedAtUtc)
                SELECT Id, ContentKey, Route, Culture, '', Renderer, Snapshot, SnapshotHtml,
                       Checksum, FinalUrl, TitleText, SummaryText, HeadingText, BodyText,
                       ContentHash, RenderStatus, LastRenderError, LastAttemptAtUtc,
                       RenderedAtUtc, CreatedAtUtc, UpdatedAtUtc
                FROM umbracoRazorSearchSnapshot;
                DROP TABLE umbracoRazorSearchSnapshot;
                ALTER TABLE umbracoRazorSearchSnapshot_new RENAME TO umbracoRazorSearchSnapshot;
                CREATE INDEX IX_umbracoRazorSearchSnapshot_ContentKey
                    ON umbracoRazorSearchSnapshot (ContentKey);
                CREATE INDEX IX_umbracoRazorSearchSnapshot_RenderedAtUtc
                    ON umbracoRazorSearchSnapshot (RenderedAtUtc);
                CREATE INDEX IX_umbracoRazorSearchSnapshot_RenderStatus
                    ON umbracoRazorSearchSnapshot (RenderStatus);
                CREATE UNIQUE INDEX IX_umbracoRazorSearchSnapshot_ContentKey_Route_Culture_Segment_Renderer
                    ON umbracoRazorSearchSnapshot (ContentKey, Route, Culture, Segment, Renderer);
                """);
            return;
        }

        migrationBuilder.DropIndex(
            name: "IX_umbracoRazorSearchSnapshot_ContentKey_Culture",
            table: "umbracoRazorSearchSnapshot");

        migrationBuilder.AddColumn<string>(
            name: "Segment",
            table: "umbracoRazorSearchSnapshot",
            maxLength: 64,
            nullable: false,
            defaultValue: string.Empty);

        migrationBuilder.CreateIndex(
            name: "IX_umbracoRazorSearchSnapshot_ContentKey_Route_Culture_Segment_Renderer",
            table: "umbracoRazorSearchSnapshot",
            columns: ["ContentKey", "Route", "Culture", "Segment", "Renderer"],
            unique: true);
    }
}
