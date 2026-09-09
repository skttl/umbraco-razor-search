using Umbraco.Community.RazorSearch.Configuration;

/// <summary>Configuration schema for Umbraco Community RazorSearch.</summary>
internal sealed class UmbracoCommunityRazorSearchSchema
{
    public UmbracoDefinition? Umbraco { get; set; }
    public sealed class UmbracoDefinition
    {
        public CommunityDefinition? Community { get; set; }
    }
    public sealed class CommunityDefinition
    {
        public RazorSearchOptions? RazorSearch { get; set; }
    }
}
