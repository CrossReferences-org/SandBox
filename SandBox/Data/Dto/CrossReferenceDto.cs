using System.Text.Json.Serialization;

namespace SandBox.Data.Dto
{
    public record CrossReferenceDto
    {
        [JsonPropertyName("verse_id")]
        public int VerseId { get; init; }

        [JsonPropertyName("sort")]
        public int Sort { get; init; }

        [JsonPropertyName("kjv")]
        public string? Kjv { get; init; }

        [JsonPropertyName("bsb")]
        public string? Bsb { get; init; }

        [JsonPropertyName("aov")]
        public string? Aov { get; init; }

        // Each inner array is one reference, which may span several verses.
        // Flattening these would lose the distinction between "Prov 8:22-24"
        // and three unrelated verses — see the dataset readme.
        [JsonPropertyName("refs")]
        public int[][]? Refs { get; init; }
    }
}
