using System.Text.Json.Serialization;

namespace SandBox.Data.Dto
{
    // These mirror the JSON files exactly and exist only to be deserialised.
    // Everything downstream uses the M_* models. When the upstream export
    // changes shape, this file and the projection in SnapshotLoader are the
    // only places that need to know.
    //
    // Properties absent from these records are ignored by System.Text.Json,
    // which is how name_fra, abbreviation_fra and the s21 anchor are dropped.

    public record BookDto
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("name_eng")]
        public string? NameEng { get; init; }
        [JsonPropertyName("name_afr")]
        public string? NameAfr { get; init; }

        [JsonPropertyName("abbreviation_eng")]
        public string? AbbreviationEng { get; init; }

        [JsonPropertyName("abbreviation_afr")]
        public string? AbbreviationAfr { get; init; }
    }
}
