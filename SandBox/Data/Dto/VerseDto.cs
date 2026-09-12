using System.Text.Json.Serialization;

namespace SandBox.Data.Dto
{
    public record VerseDto
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("book_id")]
        public int BookId { get; init; }


        [JsonPropertyName("kjv_ch")]
        public int KjvChapter { get; init; }

        [JsonPropertyName("kjv_vs")]
        public int KjvVerse { get; init; }

        [JsonPropertyName("kjv_sort")]
        public int KjvSort { get; init; }

        [JsonPropertyName("kjv_text")]
        public string? KjvText { get; init; }


        [JsonPropertyName("bsb_ch")]
        public int BsbChapter { get; init; }

        [JsonPropertyName("bsb_vs")]
        public int BsbVerse { get; init; }

        [JsonPropertyName("bsb_sort")]
        public int BsbSort { get; init; }

        [JsonPropertyName("bsb_text")]
        public string? BsbText { get; init; }


        [JsonPropertyName("aov_ch")]
        public int AovChapter { get; init; }

        [JsonPropertyName("aov_vs")]
        public int AovVerse { get; init; }

        [JsonPropertyName("aov_sort")]
        public int AovSort { get; init; }

        [JsonPropertyName("aov_text")]
        public string? AovText { get; init; }
    }
}
