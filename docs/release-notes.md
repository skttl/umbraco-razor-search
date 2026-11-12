# Umbraco 19 beta

Package version: `19.0.0-beta01`, targeting Umbraco `19.0.0-rc1` and .NET 11. This is a prerelease compatibility update, not a stable release.

- Preserve Umbraco 18 support on `v18/main` and Umbraco 17 support on `v17/main`.
- Use the Search contracts now included in `Umbraco.Cms.Core` and the Examine 4 provider.
- Use Umbraco 19 cookie authentication for management requests and the queue status stream.
- Let Umbraco register Search and its default Examine provider. Applications no longer need explicit `AddSearchCore()` and `AddExamineSearchProvider()` calls.
- Update the backoffice client, demo starter kit, EF Core dependencies, build tooling and package checks for v19.

Back up the database before upgrading an existing site. Umbraco 18 cannot reopen a database upgraded by Umbraco 19. Rebuild search indexes and RazorSearch snapshots after upgrading. Review the [Umbraco 19 upgrade notes](https://docs.umbraco.com/umbraco-cms/19.latest/get-started/upgrading-and-migrating/version-specific), including property editor migrations and imaging key requirements.

The demo starts after upgrading its database to v19, and its backoffice has been smoke-tested locally. CI passes all 84 C# tests, including database tests, and both client tests. Full rendering and indexing acceptance still requires manual verification.

The RC's static assets fail .NET 11 compression even without RazorSearch. The demo and package probes disable compression; consumers need the [temporary workaround](installation/README.md). The development npm dependency tree also reports existing advisories, including DOMPurify and Vite. Those dependency issues are outside this compatibility update.

## Previous stable release

Stable releases: 18.0.0 and 17.0.0, for the matching Umbraco major.

- Search rendered HTML through Umbraco Search, with an optional Examine companion.
- Store one snapshot per document and culture, preserving the last usable text after a rendering error.
- Rebuild in background batches, with bounded retries/history and follow-up rendering after concurrent publication.
- Search an exact configured language plus invariant content. Omitted culture searches invariant content only.
- Configure extraction with CSS selectors or published properties under `Umbraco:Community:RazorSearch`.
- Install generated JSON Schema automatically through NuGet build assets.
- Enforce document permissions and administrator-only global operations and raw HTML access.
- Support one dedicated backoffice and multiple frontends sharing SQL Server, with local Examine indexes.

## Limits

SQLite is not release-verified. Concurrent rebuild and partial-culture unpublish have produced database lock failures on Umbraco 17, including failures with private cache. SQLite remains available, but these concurrency cases are not release-verified. The observed failures remain a known limitation.

The queue is in memory and loses pending work on restart. Rebuild manually after interrupted work or template/shared-content/extraction changes. Multiple active backoffice servers, segments, member-personalized results and dedicated fuzzy/wildcard APIs are not supported. The backoffice displays expected index content rather than live provider fields. Other providers require their own verification.

The earlier development API and snapshot schema were never released. Breaking changes are intentional; use a fresh disposable development database instead of expecting an upgrade from that schema.
