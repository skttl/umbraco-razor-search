# Stock Umbraco SQLite diagnostic

This optional diagnostic contains **no RazorSearch package reference or composer**. It preserves a reduced reproduction of the shared-cache contention observed while evaluating SQLite. SQLite support is deferred beyond the first RazorSearch beta; this executable is not a passing release check.

The only packages are Umbraco CMS 17.6.2 and ASP.NET Core TestHost 10.0.12. TestServer opens no TCP listener. Each run creates a fresh Temp directory/database, generates installation credentials in memory, waits for content-routing readiness and publishes invariant and English/Danish content.

The driver concurrently calls `IContentService.GetPagedDescendants` and `IContentService.Unpublish(content, "da-DK")`, then republishes Danish before repeating. An operation must finish within 60 seconds; the whole process has a 180-second failure deadline. Artifacts remain in the printed Temp directory. Existing sites and databases are untouched.

```powershell
dotnet run --project tests/diagnostics/sqlite-stock/StockCms.csproj -- Shared
```

The code accepts `Private` for a future comparison, but the preserved stock-CMS runs tested **Shared only**. A separate RazorSearch diagnostic observed another failure mode with private cache; that is not evidence that this stock-CMS case fails with private cache.

On 2026-09-09, two fresh shared-cache runs stalled during the first concurrent read/unpublish. The first exceeded a 20-second operation bound; the repeat exceeded 60 seconds. Managed stacks from the repeat showed the writer in `DocumentRepository.PersistUpdatedItem` under `Unpublish` and the reader in `GetPagedDescendants`. The generated dependency manifest contained no RazorSearch dependency. These observations isolate that shared-cache failure from the package, but do not establish a fix or a passing SQLite acceptance result.
