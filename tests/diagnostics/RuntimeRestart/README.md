# Runtime restart diagnostic

This standalone diagnostic uses the installed NuGet packages, a real SQL Server database, Umbraco CMS, EF Core and Examine. ASP.NET Core TestServer executes HTTP requests in memory: no TCP listener is created. Every process has a 360-second deadline and exits on completion or failure. It never stops another process.

Copy this directory to a new temporary directory. Supply an `appsettings.json` following [the acceptance fixture configuration](../../acceptance/README.md), with a newly created, isolated SQL Server database, `Acceptance:Enabled=true`, `Acceptance:Role=SchedulingPublisher` and `Acceptance:PublicBaseUrl=http://localhost`. Set `Umbraco:CMS:WebRouting:UmbracoApplicationUrl` and `Umbraco:Community:RazorSearch:HttpRenderer:RenderBaseAddress` to `http://localhost`. Keep the supplied view present before building. The diagnostic driver waits for `IContentRoutingReadiness.IsReady` before reading or publishing content; `app.StartAsync()` alone does not guarantee that Umbraco has finished background initialization. This is a test-driver requirement, not additional RazorSearch application configuration. Restore the candidate packages from their local package directory into a fresh NuGet cache.

Use `--phase=bootstrap` on a fresh database and content root to verify publication, rendering and search in the first process without a restart.

Run the `seed` phase, then the `recover` phase as separate foreground processes using `dotnet run --project RuntimeRestart.csproj -- --phase=seed` and `dotnet run --project RuntimeRestart.csproj -- --phase=recover`.

The seed phase creates invariant and English/Danish content, renders three snapshots, verifies exact-culture search, and holds a later render request. It writes `restart-manifest.json`, then deliberately exits its own process with running and queued work. Recovery starts a new CMS host against the same database, requires an empty queue, compares snapshot IDs/text/timestamps with the manifest and completes a manual rebuild without failed jobs.

For the `subscriber` phase, create another temporary content-root directory with a copy of the configuration, view and the latest manifest. Set `Acceptance:Role=Subscriber`. Its index directory must not exist. Run the compiled diagnostic DLL with that new working directory and `--phase=subscriber`. This verifies that a fresh index is populated from existing snapshots, exact-culture search returns invariant content where appropriate, URLs remain public, and the render queue stays empty.

The first search may wait up to 180 seconds for index visibility. Assertions fail on timeout. A previously interrupted seed run is idempotent when exactly the two diagnostic content types already exist.

This checks real process loss, persistence, manual recovery and fresh-index bootstrap. It does not replace TCP renderer, multi-node cache-distribution or backoffice browser acceptance. Do not deploy this diagnostic with a website.


