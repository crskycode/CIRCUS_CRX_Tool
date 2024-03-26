using System.Text.Json.Serialization;

namespace CIRCUS_CRX
{
    [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(CRXG.Metadata))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
