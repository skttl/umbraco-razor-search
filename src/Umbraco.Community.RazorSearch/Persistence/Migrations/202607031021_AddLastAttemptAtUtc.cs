using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Umbraco.Community.RazorSearch.Persistence.Migrations;

[DbContext(typeof(RazorSearchDbContext))]
[Migration("202607031021_AddLastAttemptAtUtc")]
public sealed class AddLastAttemptAtUtc : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastAttemptAtUtc",
            table: "umbracoRazorSearchSnapshot",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastAttemptAtUtc",
            table: "umbracoRazorSearchSnapshot");
    }
}
