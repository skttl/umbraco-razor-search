# Installed-package runtime fixture

This app is a local acceptance tool, excluded from the solution and NuGet packages. It deliberately exposes mutation endpoints for an isolated test database. Startup requires `Acceptance:Enabled=true`; `/acceptance` endpoints reject remote clients. Do not deploy it with a website.

Copy this directory into a temporary folder. Supply a `NuGet.Config` with the freshly packed local package directory and nuget.org. Use a unique prerelease package version or an isolated NuGet package cache when testing repeated local builds.

Supply `appsettings.json` in that temporary folder:

```json
{
  "Urls": "http://localhost:51881",
  "Acceptance": { "Enabled": true, "Role": "Single", "PublicBaseUrl": "http://localhost:51881" },
  "ConnectionStrings": {
    "umbracoDbDSN": "Server=.\\SQLEXPRESS;Database=RazorSearch_Acceptance_UNIQUE;Integrated Security=true;TrustServerCertificate=true",
    "umbracoDbDSN_ProviderName": "Microsoft.Data.SqlClient"
  },
  "Umbraco": {
    "CMS": {
      "WebRouting": { "UmbracoApplicationUrl": "http://localhost:51881" },
      "Unattended": {
        "InstallUnattended": true, "UpgradeUnattended": true,
        "UnattendedUserName": "Acceptance Admin",
        "UnattendedUserEmail": "acceptance@example.invalid",
        "UnattendedUserPassword": "SET-A-LOCAL-TEST-PASSWORD"
      },
      "ModelsBuilder": { "ModelsMode": "Nothing" }
    },
    "Community": {
      "RazorSearch": {
        "RenderRequestToken": "SET-A-SHARED-TEST-TOKEN",
        "HttpRenderer": { "RenderBaseAddress": "http://localhost:51881" },
        "SnapshotExtraction": { "BodySources": [{ "Type": "selector", "Selector": "main" }] }
      }
    }
  }
}
```

For SQLite, use an absolute path such as `Data Source=C:/temporary/acceptance/acceptance.db` and provider `Microsoft.Data.Sqlite`; create the parent directory first. Never reuse an existing site's connection string. Create the isolated SQL Server database before starting the app.

Build with `dotnet build -p:UmbracoVersion=18.1.1 -p:RazorSearchVersion=18.0.0-beta.1`; supply the corresponding 17 versions to test that line. Start the resulting DLL from the temporary app directory.

Poll `GET /acceptance/health` until `ready` is true before seeding or mutating content. Stop if `bootFailed` is true. A listening host or `RuntimeLevel.Run` alone does not establish readiness: unattended upgrades can still be running application-starting handlers, including RazorSearch migrations. Mutation endpoints return HTTP 503 until routing is ready.

Endpoints:

- `POST /acceptance/seed` creates a template, invariant/variant document types, an invariant document and a Danish/English document. It returns document keys. Repeated calls create additional documents.
- `POST /acceptance/domains/{variantKey}` assigns `/en` and `/da` under the configured public origin.
- `POST /acceptance/rebuild` accepts a full background rebuild as the local admin.
- `GET /acceptance/status` reports render queue and rebuild discovery progress.
- `GET /acceptance/snapshots/{key}` returns persisted snapshots and attempts.
- `GET /acceptance/search?q=commonword&culture=da-DK` exercises public search. Omit culture for invariant only; use `englishonly`/`danishonly` to test isolation.
- `GET /acceptance/index` reports the raw local Examine document count and ten sample documents.
- `POST /acceptance/mutate/{key}?action=draft&name=changed`, `action=publish`, or `action=unpublish` exercise lifecycle changes.
- `action=publish-culture&culture=da-DK&name=changed` and `action=unpublish-culture&culture=da-DK` change one language while leaving the other published culture available.
- `POST /acceptance/failure?enabled=true` makes render requests return HTTP 503. Disable it with `enabled=false`, then rebuild to verify recovery without losing the previous usable snapshot.
- `POST /acceptance/bulk?count=5001` seeds and publishes 5,001 invariant documents asynchronously. Poll `GET /acceptance/bulk`, then run a full rebuild and check its discovery/queue counts plus database snapshot counts. Large seeds can take several minutes.

Include the bundled `Views/acceptance.cshtml` when copying the fixture. It is compiled before the first seed, so initial publication can render without a restart. Publication of a new document must create a snapshot without a restart or manual rebuild.

For load balancing, use two separate temporary app directories and index folders with the same SQL database. Set the backoffice role to `SchedulingPublisher`; set the second process to `Subscriber`, with a different listening port but the same public origin and internal render destination pointing at the backoffice. Copy the generated Views directory to the frontend. Publish/unpublish on the backoffice and poll each process's search and raw local index until distributed changes arrive. Subscriber queue must remain empty.
