# First beta

Release candidate: 17.0.0-beta.1 for Umbraco 17. Publication follows completion of the release acceptance matrix.

- Search rendered HTML through Umbraco Search, with an optional Examine companion.
- Store one snapshot per document and culture, preserving the last usable text after a rendering error.
- Rebuild in background batches, with bounded retries/history and follow-up rendering after concurrent publication.
- Search an exact configured language plus invariant content. Omitted culture searches invariant content only.
- Configure extraction with CSS selectors or published properties under `Umbraco:Community:RazorSearch`.
- Install generated JSON Schema automatically through NuGet build assets.
- Enforce document permissions and administrator-only global operations and raw HTML access.
- Support one dedicated backoffice and multiple frontends sharing SQL Server, with local Examine indexes.

## Limits

SQLite verification is deferred for the first beta. Concurrent rebuild and partial-culture unpublish have produced database lock failures on Umbraco 17, including failures with private cache. SQLite remains available, but these concurrency cases are not release-verified. This limitation is accepted for the beta and is not a claim that the observed failures have been fixed.

The queue is in memory and loses pending work on restart. Rebuild manually after interrupted work or template/shared-content/extraction changes. Multiple active backoffice servers, segments, member-personalized results and dedicated fuzzy/wildcard APIs are outside this beta. The backoffice displays expected index content rather than live provider fields. Other providers require their own verification.

The earlier development API and snapshot schema were never released. Breaking changes are intentional; use a fresh disposable development database instead of expecting an upgrade from that schema.

A successful build alone does not establish release readiness; verify the installed packages and the documented acceptance scenarios before publishing.
