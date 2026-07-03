using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Umbraco.Community.RazorSearch.Persistence.Migrations;

[DbContext(typeof(RazorSearchDbContext))]
[Migration("202607020001_CreateRazorSearchSnapshot")]
public sealed class CreateRazorSearchSnapshot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "umbracoRazorSearchSnapshot",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                ContentKey = table.Column<Guid>(nullable: false),
                Route = table.Column<string>(maxLength: 2048, nullable: false),
                Culture = table.Column<string>(maxLength: 32, nullable: false),
                Segment = table.Column<string>(maxLength: 64, nullable: false),
                Renderer = table.Column<string>(maxLength: 128, nullable: false),
                Snapshot = table.Column<string>(nullable: false),
                Checksum = table.Column<string>(maxLength: 128, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_umbracoRazorSearchSnapshot", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_umbracoRazorSearchSnapshot_ContentKey",
            table: "umbracoRazorSearchSnapshot",
            column: "ContentKey");

        migrationBuilder.CreateIndex(
            name: "IX_umbracoRazorSearchSnapshot_ContentKey_Route_Culture_Segment_Renderer",
            table: "umbracoRazorSearchSnapshot",
            columns: ["ContentKey", "Route", "Culture", "Segment", "Renderer"],
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "umbracoRazorSearchSnapshot");
}
