namespace SandBox.Data
{
    public class CrossReference
    {
        public int ID { get; init; }

        public int SourceVerseID { get; init; }

        // Orders the anchors within a verse. Matters for the KJV, where the
        // Treasury locates anchors sequentially: the second anchor's phrase is
        // the next occurrence after the first. The other translations match
        // their anchor against the verse once, so order is incidental there.
        public int SortOrder { get; init; }

        public string KjvAnchor { get; init; } = "";
        public string BsbAnchor { get; init; } = "";
        public string AovAnchor { get; init; } = "";

        public IReadOnlyList<ReferenceRange> ReferenceRanges { get; init; } = [];
    }
}
