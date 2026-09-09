# Contributing to RazorSearch

`main` follows Umbraco 18. `v17/main` maintains 17. When the next major is adopted, branch the previous implementation to `v18/main`, then upgrade `main`. Port common fixes and verify both lines.

Use .NET SDK 10, Node.js 24.13+ and npm 11+. Node can be installed in a local directory; no global toolchain change is required.

From the repository root:

```powershell
./build/Validate.ps1
```

This runs `npm ci`, type checking, client build, solution build, regression tests, both NuGet packs and package-content checks. Local versions come from `Directory.Build.props` and must match `Client/package.json`. CI runs the same script. Release tags must exactly match that version and the CMS dependency major.

## Demo

```powershell
dotnet run --project src/Umbraco.Community.RazorSearch.Demo
```

The development settings use an unattended SQLite installation. The checked-in account is for a disposable local demo only. Use a fresh database for the first beta schema. Never reset an existing database without checking that it is disposable.

The demo includes the Clean starter kit. Keep the RazorSearch search template when updating it. Run a manual RazorSearch rebuild after the starter content is published. For rendering and backoffice work, verify the running demo as well as tests.

## Database tests

SQLite migration tests run in the regression suite. SQL Server tests use `RAZORSEARCH_TEST_SQLSERVER` when provided. Use a dedicated disposable test database with an unambiguous name, never a site's production or development database.

NuGet acceptance must use installed packages rather than project references. Verify direct core and companion-only installation, schema copying/registration, client assets and the release matrix in `.agents/docs/release-fix-plan.md`.
