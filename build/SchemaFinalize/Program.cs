using System.Text.Json;
using System.Text.Json.Nodes;

// Umbraco's generator leaves objects open. Close only our own options and describe their source union.
JsonNode schema = JsonNode.Parse(File.ReadAllText(args[0]))!;
JsonObject definitions = schema["definitions"]!.AsObject();
foreach (var definition in definitions)
{
    if (definition.Key.StartsWith("RazorSearch", StringComparison.Ordinal))
        definition.Value!["additionalProperties"] = false;
}
definitions["RazorSearchSnapshotSourceDefinition"]!["oneOf"] = JsonNode.Parse("""
[
  {"required":["Selector"],"properties":{"Type":{"enum":["selector"]},"Selector":{"type":"string","minLength":1},"Alias":{"type":"null"}}},
  {"required":["Type","Alias"],"properties":{"Type":{"enum":["property"]},"Alias":{"type":"string","minLength":1},"Selector":{"type":"null"},"Attribute":{"type":"null"}}}
]
""");
File.WriteAllText(args[0], schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
