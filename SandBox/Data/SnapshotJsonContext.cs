using SandBox.Data.Dto;
using System.Text.Json.Serialization;

namespace SandBox.Data
{
    // Source-generated serialisation: no reflection warm-up at startup, and it
    // keeps the loader trim-friendly if this ever gets published as a single file.
    [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(BookDto[]))]
    [JsonSerializable(typeof(VerseDto[]))]
    [JsonSerializable(typeof(CrossReferenceDto[]))]
    public partial class SnapshotJsonContext : JsonSerializerContext
    {
    }
}
