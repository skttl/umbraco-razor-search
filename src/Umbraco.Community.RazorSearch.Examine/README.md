# Umbraco.Community.RazorSearch.Examine

`Umbraco.Community.RazorSearch.Examine` is the Examine companion package for `Umbraco.Community.RazorSearch`.

Install this package when your host uses `Umbraco.Cms.Search.Provider.Examine`.

The companion package:

- creates the internal Lucene/Examine index used by RazorSearch
- configures the required field definitions for RazorSearch fulltext and filter fields
- keeps the RazorSearch package provider-agnostic

You still need to enable `AddSearchCore()` and `AddExamineSearchProvider()` in the host application.
