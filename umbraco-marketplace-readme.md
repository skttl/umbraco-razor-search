# RazorSearch

Search rendered Umbraco pages with Umbraco Search. RazorSearch stores an HTML snapshot for each published page, extracts its title, headings and body text, and adds those fields to a dedicated search index.

## Installation

For an Examine-based Umbraco Search setup, install the Examine companion package:

```powershell
dotnet add package Umbraco.Community.RazorSearch.Examine
```

Register Umbraco Search and the Examine provider in `Program.cs`:

```csharp
using Umbraco.Cms.Search.Core.DependencyInjection;
using Umbraco.Cms.Search.Provider.Examine.DependencyInjection;

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddSearchCore()
    .AddExamineSearchProvider()
    .Build();
```

The companion package includes the core RazorSearch package and creates the physical Examine index. Start the site, sign in to the backoffice, and run a rebuild from the RazorSearch dashboard to index existing published content.

RazorSearch core is provider-agnostic. To use another Umbraco Search provider, install `Umbraco.Community.RazorSearch` and configure that provider in the consuming application.

## What RazorSearch does

- Renders and stores the current HTML snapshot for each published page and culture.
- Extracts searchable text from titles, headings, body content and configured selectors.
- Updates snapshots asynchronously after publication.
- Provides a backoffice dashboard for monitoring rendering and rebuilding content.
- Removes unpublished, deleted and excluded content from search.

## Configuration

RazorSearch settings live under `Umbraco:Community:RazorSearch`. For example:

```json
{
  "Umbraco": {
    "Community": {
      "RazorSearch": {
        "SnapshotExtraction": {
          "BodySources": [
            { "Type": "selector", "Selector": "main" }
          ]
        }
      }
    }
  }
}
```

See the [full documentation](https://github.com/skttl/umbraco-razor-search/blob/main/docs/README.md) for configuration, usage, indexing, troubleshooting and customization.
