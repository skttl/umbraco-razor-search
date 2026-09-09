using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Umbraco.Community.RazorSearch.Persistence.Migrations;

[DbContext(typeof(RazorSearchDbContext))]
[Migration("202607020002_ExpandRazorSearchSnapshot")]
public sealed class ExpandRazorSearchSnapshot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BodyText",
            table: "umbracoRazorSearchSnapshot",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ContentHash",
            table: "umbracoRazorSearchSnapshot",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "FinalUrl",
            table: "umbracoRazorSearchSnapshot",
            maxLength: 2048,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "HeadingText",
            table: "umbracoRazorSearchSnapshot",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LastRenderError",
            table: "umbracoRazorSearchSnapshot",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "RenderedAtUtc",
            table: "umbracoRazorSearchSnapshot",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RenderStatus",
            table: "umbracoRazorSearchSnapshot",
            maxLength: 32,
            nullable: false,
            defaultValue: "Success");

        migrationBuilder.AddColumn<string>(
            name: "SummaryText",
            table: "umbracoRazorSearchSnapshot",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TitleText",
            table: "umbracoRazorSearchSnapshot",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_umbracoRazorSearchSnapshot_RenderedAtUtc",
            table: "umbracoRazorSearchSnapshot",
            column: "RenderedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_umbracoRazorSearchSnapshot_RenderStatus",
            table: "umbracoRazorSearchSnapshot",
            column: "RenderStatus");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_umbracoRazorSearchSnapshot_RenderedAtUtc",
            table: "umbracoRazorSearchSnapshot");

        migrationBuilder.DropIndex(
            name: "IX_umbracoRazorSearchSnapshot_RenderStatus",
            table: "umbracoRazorSearchSnapshot");

        migrationBuilder.DropColumn(name: "BodyText", table: "umbracoRazorSearchSnapshot");
        migrationBuilder.DropColumn(name: "ContentHash", table: "umbracoRazorSearchSnapshot");
        migrationBuilder.DropColumn(name: "FinalUrl", table: "umbracoRazorSearchSnapshot");
        migrationBuilder.DropColumn(name: "HeadingText", table: "umbracoRazorSearchSnapshot");
        migrationBuilder.DropColumn(name: "LastRenderError", table: "umbracoRazorSearchSnapshot");
        migrationBuilder.DropColumn(name: "RenderedAtUtc", table: "umbracoRazorSearchSnapshot");
        migrationBuilder.DropColumn(name: "RenderStatus", table: "umbracoRazorSearchSnapshot");
        migrationBuilder.DropColumn(name: "SummaryText", table: "umbracoRazorSearchSnapshot");
        migrationBuilder.DropColumn(name: "TitleText", table: "umbracoRazorSearchSnapshot");
    }
}
