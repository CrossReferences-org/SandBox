namespace SandBox.Data
{
    public class Verse
    {
        public int ID { get; init; }
        public int BookNumber { get; init; }

        // A verse may be split across several records where versification
        // differs. Sort is per-translation because the split falls in a
        // different place in each: order by it, then concatenate the text.
        public int KjvChapter { get; init; }
        public int KjvVerse { get; init; }
        public int KjvSort { get; init; }
        public string KjvText { get; init; } = "";

        public int BsbChapter { get; init; }
        public int BsbVerse { get; init; }
        public int BsbSort { get; init; }
        public string BsbText { get; init; } = "";

        public int AovChapter { get; init; }
        public int AovVerse { get; init; }
        public int AovSort { get; init; }
        public string AovText { get; init; } = "";
    }
}
