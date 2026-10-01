# First stable release

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
