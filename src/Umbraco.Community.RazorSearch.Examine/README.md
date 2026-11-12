# RazorSearch Examine companion

The companion creates the physical Examine index and field definitions for `Umbraco.Community.RazorSearch`. It includes core transitively and targets the matching Umbraco major.

```powershell
dotnet add package Umbraco.Community.RazorSearch.Examine --version 19.0.0-rc.1
```

Umbraco 19 registers Search and its default Examine provider automatically. This package does not take over the application's published-content index.

Configuration belongs under `Umbraco:Community:RazorSearch`. Core supplies the generated appsettings-schema and registers it automatically at build, including through this transitive installation.

See the complete [installation example](https://github.com/skttl/umbraco-razor-search/blob/main/docs/installation/README.md), [configuration](https://github.com/skttl/umbraco-razor-search/blob/main/docs/configuration/README.md), and [load-balancing guidance](https://github.com/skttl/umbraco-razor-search/blob/main/docs/indexing/README.md).
