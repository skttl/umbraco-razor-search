namespace Umbraco.Community.RazorSearch;

public static class Constants
{
    public const string ExtensionName = "RazorSearch";
    public const string ConfigurationSection = "Umbraco:Community:RazorSearch";
    internal const string PublicSchemeHeaderName = "X-RazorSearch-Public-Scheme";
    public const string RenderRequestHeaderName = "X-RazorSearch-Render";
    public const string ApiName = "RazorSearch";
    public const string TitleFieldName = "RazorSearch_Title";
    public const string SummaryFieldName = "RazorSearch_Summary";
    public const string HeadingFieldName = "RazorSearch_Heading";
    public const string ContentFieldName = "RazorSearch_Content";

    internal static class InternalIndex
    {
        public const string Alias = "Umb_RazorSearch";
        public const string ContentTypeAliasFieldName = "RazorSearch_ContentTypeAlias";
        public const string ExcludedFlagFieldName = "RazorSearch_Excluded";
    }
}
