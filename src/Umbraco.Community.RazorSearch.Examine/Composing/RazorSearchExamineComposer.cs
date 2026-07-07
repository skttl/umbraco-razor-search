using Examine;
using Examine.Lucene.Directories;
using Examine.Lucene.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Community.RazorSearch.Examine.Configuration;

namespace Umbraco.Community.RazorSearch.Examine.Composing;

public sealed class RazorSearchExamineComposer : IComposer
{
    private const string ExamineSettingsSection = "Umbraco:CMS:Search:Examine";
    private const string ActiveSuffixA = "_a";
    private const string ActiveSuffixB = "_b";

    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.PostConfigure<Umbraco.Cms.Search.Provider.Examine.Configuration.FieldOptions>(
            RazorSearchExamineFieldOptions.Configure);

        if (ZeroDowntimeIndexingEnabled(builder.Config))
        {
            builder.Services.AddExamineLuceneIndex<LuceneIndex, ConfigurationEnabledDirectoryFactory>(
                RazorSearchExamineConstants.IndexAlias + ActiveSuffixA,
                _ => { });
            builder.Services.AddExamineLuceneIndex<LuceneIndex, ConfigurationEnabledDirectoryFactory>(
                RazorSearchExamineConstants.IndexAlias + ActiveSuffixB,
                _ => { });
            return;
        }

        builder.Services.AddExamineLuceneIndex<LuceneIndex, ConfigurationEnabledDirectoryFactory>(
            RazorSearchExamineConstants.IndexAlias,
            _ => { });
    }

    private static bool ZeroDowntimeIndexingEnabled(IConfiguration configuration) =>
        configuration.GetSection(ExamineSettingsSection).GetValue<bool>("ZeroDowntimeIndexing");
}
