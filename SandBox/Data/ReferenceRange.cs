namespace SandBox.Data
{
    /// <summary>
    /// One reference. Usually a single verse, sometimes a span meant to be read
    /// as a unit (Prov 8:22-24). The snapshot already carries expanded verse
    /// IDs, so no range parsing happens at load time.
    /// </summary>
    public class ReferenceRange
    {
        public int[] VerseIDs { get; init; } = [];
    }
}
