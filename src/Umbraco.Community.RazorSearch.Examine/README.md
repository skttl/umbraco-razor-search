# RazorSearch Examine companion

The companion creates the physical Examine index and field definitions for `Umbraco.Community.RazorSearch`. It includes core transitively and targets the matching Umbraco major.

```powershell
dotnet add package Umbraco.Community.RazorSearch.Examine --version 17.0.0-beta.1
```

The consuming application must still call `AddSearchCore()` and `AddExamineSearchProvider()`. This package does not take over the application's published-content index.

Configuration belongs under `Umbraco:Community:RazorSearch`. Core supplies the generated appsettings-schema and registers it automatically at build, including through this transitive installation.

See the complete [installation example](https://github.com/skttl/umbraco-razor-search/blob/main/docs/installation/README.md), [configuration](https://github.com/skttl/umbraco-razor-search/blob/main/docs/configuration/README.md), and [load-balancing guidance](https://github.com/skttl/umbraco-razor-search/blob/main/docs/indexing/README.md).
