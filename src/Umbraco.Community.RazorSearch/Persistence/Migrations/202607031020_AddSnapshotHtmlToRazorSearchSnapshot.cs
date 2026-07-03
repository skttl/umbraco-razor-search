using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Umbraco.Community.RazorSearch.Persistence.Migrations;

[DbContext(typeof(RazorSearchDbContext))]
[Migration("202607031020_AddSnapshotHtmlToRazorSearchSnapshot")]
public sealed class AddSnapshotHtmlToRazorSearchSnapshot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SnapshotHtml",
            table: "umbracoRazorSearchSnapshot",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SnapshotHtml",
            table: "umbracoRazorSearchSnapshot");
    }
}
