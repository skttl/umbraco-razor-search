using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.RazorSearch.Persistence;
using Umbraco.Community.RazorSearch.Persistence.Models;

namespace Umbraco.Community.RazorSearch.Tests;

public sealed class SnapshotDatabaseTests
{
    public static IEnumerable<object[]> Providers()
    {
        yield return ["SQLite", "Data Source=:memory:"];
        string? sql = Environment.GetEnvironmentVariable("RAZORSEARCH_TEST_SQLSERVER");
        if (!string.IsNullOrWhiteSpace(sql)) yield return ["SQLServer", sql];
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Migrations_support_long_urls_titles_and_unique_content_culture(string provider, string connectionString)
    {
        await using var sqlite = new SqliteConnection("Data Source=:memory:");
        var options = new DbContextOptionsBuilder<RazorSearchDbContext>();
        if (provider == "SQLite") { await sqlite.OpenAsync(); options.UseSqlite(sqlite); }
        else options.UseSqlServer(connectionString);
        await using var context = new RazorSearchDbContext(options.Options);
        await context.Database.MigrateAsync();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
        Guid key = Guid.NewGuid();
        var entity = Snapshot(key, "da-dk", new string('a', 1900));
        context.Add(entity);
        context.Add(Snapshot(key, "", "/invariant"));
        context.Add(Snapshot(key, "en-us", "/english"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var stored = await context.RazorSearchSnapshots.SingleAsync(x => x.Id == entity.Id);
        Assert.Equal(1900, stored.Route.Length);
        Assert.Equal(1200, stored.TitleText!.Length);
        Assert.Equal(3, await context.RazorSearchSnapshots.CountAsync(x => x.ContentKey == key));
        context.Add(Snapshot(key, "da-dk", "/another-route"));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Migrations_add_last_attempt_column_when_expand_migration_was_already_applied()
    {
        await using var sqlite = new SqliteConnection("Data Source=:memory:");
        await sqlite.OpenAsync();
        var options = new DbContextOptionsBuilder<RazorSearchDbContext>().UseSqlite(sqlite).Options;
        await using var context = new RazorSearchDbContext(options);

        await context.Database.ExecuteSqlRawAsync("""
            CREATE TABLE __EFMigrationsHistory (
                MigrationId TEXT NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY,
                ProductVersion TEXT NOT NULL
            );
            CREATE TABLE umbracoRazorSearchSnapshot (
                Id TEXT NOT NULL CONSTRAINT PK_umbracoRazorSearchSnapshot PRIMARY KEY,
                ContentKey TEXT NOT NULL,
                Route TEXT NOT NULL,
                Culture TEXT NOT NULL,
                Segment TEXT NOT NULL,
                Renderer TEXT NOT NULL,
                Snapshot TEXT NOT NULL,
                Checksum TEXT NULL,
                BodyText TEXT NULL,
                ContentHash TEXT NULL,
                FinalUrl TEXT NULL,
                HeadingText TEXT NULL,
                LastRenderError TEXT NULL,
                RenderedAtUtc TEXT NULL,
                RenderStatus TEXT NOT NULL DEFAULT 'Success',
                SummaryText TEXT NULL,
                TitleText TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IX_umbracoRazorSearchSnapshot_ContentKey_Route_Culture_Segment_Renderer
                ON umbracoRazorSearchSnapshot (ContentKey, Route, Culture, Segment, Renderer);
            INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
            VALUES ('202607020001_CreateRazorSearchSnapshot', '10.0.12');
            INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
            VALUES ('202607020002_ExpandRazorSearchSnapshot', '10.0.12');
            """);

        var columnsBeforeUpgrade = await ColumnNames(context);
        Assert.DoesNotContain("LastAttemptAtUtc", columnsBeforeUpgrade);

        await context.Database.MigrateAsync();

        var columnsAfterUpgrade = await ColumnNames(context);
        Assert.Contains("LastAttemptAtUtc", columnsAfterUpgrade);
        Assert.DoesNotContain("Segment", columnsAfterUpgrade);
    }

    private static async Task<List<string>> ColumnNames(RazorSearchDbContext context)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT name FROM pragma_table_info('umbracoRazorSearchSnapshot')";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync()) columns.Add(reader.GetString(0));
        return columns;
    }

    private static RazorSearchSnapshotEntity Snapshot(Guid key, string culture, string route) => new()
    {
        Id = Guid.NewGuid(), ContentKey = key, Culture = culture, Route = route, Renderer = "http",
        Snapshot = "text", TitleText = new string('t', 1200), CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow, LastAttemptAtUtc = DateTimeOffset.UtcNow,
    };
}
