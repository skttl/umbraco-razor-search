using Umbraco.Cms.Search.Provider.Examine.Configuration;

namespace Umbraco.Community.RazorSearch.Examine.Configuration;

internal sealed class RazorSearchExamineFieldOptions
{
    public static void Configure(FieldOptions options)
    {
        List<FieldOptions.Field> existingFields = options.Fields.ToList();

        AddIfMissing(existingFields, Constants.TitleFieldName, FieldValues.TextsR1);
        AddIfMissing(existingFields, Constants.HeadingFieldName, FieldValues.TextsR2);
        AddIfMissing(existingFields, Constants.ContentFieldName, FieldValues.Texts);
        AddIfMissing(existingFields, RazorSearchExamineConstants.ContentTypeAliasFieldName, FieldValues.Keywords);
        AddIfMissing(existingFields, RazorSearchExamineConstants.ExcludedFlagFieldName, FieldValues.Integers);

        options.Fields = existingFields.ToArray();
    }

    private static void AddIfMissing(
        ICollection<FieldOptions.Field> fields,
        string propertyName,
        FieldValues fieldValues)
    {
        if (fields.Any(x => x.PropertyName == propertyName && x.FieldValues == fieldValues))
        {
            return;
        }

        fields.Add(
            new FieldOptions.Field
            {
                PropertyName = propertyName,
                FieldValues = fieldValues,
            });
    }
}
