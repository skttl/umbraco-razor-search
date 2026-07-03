# Agents.md

This repository contains the `Umbraco.Community.RazorSearch` package and a demo Umbraco site used to exercise it locally.

## Purpose

RazorSearch stores rendered HTML snapshots, extracts searchable text from them, and contributes those fields to Umbraco Search through `IContentIndexer`.

The package is provider-agnostic:

- it does not call `AddSearchCore()` for consumers
- it does not register a search provider for consumers
- it does not own the consuming app's published-content index configuration

## Repository Layout

- `src/Umbraco.Community.RazorSearch`: main package source
- `src/Umbraco.Community.RazorSearch/Client`: Umbraco backoffice client built with Vite and TypeScript
- `src/Umbraco.Community.RazorSearch.Demo`: local demo site referencing the package project
- `docs`: package documentation published from the root README
- `assets`: package assets such as the NuGet icon
- `.github/CONTRIBUTING.md`: short contributor setup notes
- `.github/workflows/release.yml`: release pipeline for build, pack, and NuGet publish

## Toolchain

- .NET SDK `10`
- Node.js `22+`
- npm for the backoffice client

## Common Commands

Run commands from the repository root unless a command says otherwise.

### Restore and build

```powershell
dotnet build Umbraco.Community.RazorSearch.slnx
```

### Build the backoffice client

```powershell
cd src/Umbraco.Community.RazorSearch/Client
npm install
npm run build
```

Use this during active client work:

```powershell
cd src/Umbraco.Community.RazorSearch/Client
npm run watch
```

### Type-check the client

```powershell
cd src/Umbraco.Community.RazorSearch/Client
npm run check
```

### Run the demo site

```powershell
dotnet run --project src/Umbraco.Community.RazorSearch.Demo/Umbraco.Community.RazorSearch.Demo.csproj
```

### Pack the NuGet package

```powershell
dotnet pack src/Umbraco.Community.RazorSearch/Umbraco.Community.RazorSearch.csproj -c Release
```

## Validation Expectations

Before finishing meaningful code changes:

- build the backoffice client if anything under `Client` changed
- run `npm run check` if TypeScript changed
- run `dotnet build Umbraco.Community.RazorSearch.slnx`
- run the demo site for behavior that depends on rendering, indexing, migrations, or backoffice UI

There is currently no separate automated test project in this repository, so build and demo verification are the main safety nets.

## Package-Specific Guardrails

- Keep the package provider-agnostic. Do not add automatic search-provider registration to the library.
- Preserve the contract that consuming apps must bootstrap Umbraco Search themselves with `AddSearchCore()`.
- If changing rendering or snapshot extraction, make sure docs under `docs` and the package README still match behavior.
- If changing EF Core persistence or migrations, verify the demo app still starts cleanly and the snapshot table can be created or updated.
- If changing backoffice management features, remember the release pipeline builds the Vite client before `dotnet pack`.
- The package README inside `src/Umbraco.Community.RazorSearch/README.md` is packed into the NuGet package. Keep it aligned with the root README when installation or behavior changes.

## Editing Guidance

- Prefer focused changes over broad refactors.
- Avoid renaming public package types unless the change is intentional and documented.
- Treat warnings as errors for the main package project.
- Update docs in the same change when public configuration, installation, or usage changes.

## Release Notes Context

The GitHub Actions release workflow:

- runs on version tags
- installs Node.js `22`
- builds the client in `src/Umbraco.Community.RazorSearch/Client`
- packs the main project
- pushes the package to NuGet

If you change build inputs, packaging behavior, or client output expectations, review `.github/workflows/release.yml` too.
