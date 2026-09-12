using SandBox.Data.Dto;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace SandBox.Data
{
    internal sealed class SnapshotLoader
    {
        public const string BooksFile = "bible_books.json";
        public const string VersesFile = "bible_verses.json";
        public const string CrossReferencesFile = "cross_references.json";

        private readonly string _folder;

        public SnapshotLoader(string folder) => _folder = folder;

        public (List<Book> books, List<Verse> verses, List<CrossReference> xrefs) LoadAll()
        {
            List<Book> books = LoadBooks();
            List<Verse> verses = LoadVerses();

            // The snapshot comes from upstream, so a reference can point at a
            // verse ID that isn't in this export. Dropping those here means
            // nothing downstream has to guard its verse lookups.
            HashSet<int> knownVerseIds = [.. verses.Select(v => v.ID)];

            List<CrossReference> xrefs = LoadCrossReferences(knownVerseIds);

            return (books, verses, xrefs);
        }

        private List<Book> LoadBooks()
        {
            BookDto[] dtos = Read(BooksFile, SnapshotJsonContext.Default.BookDtoArray);

            var list = new List<Book>(dtos.Length);
            foreach (BookDto d in dtos)
                list.Add(new Book
                {
                    ID = d.Id,
                    NameEng = d.NameEng ?? "",
                    NameAfr = d.NameAfr ?? "",
                    AbbreviationEng = d.AbbreviationEng ?? "",
                    AbbreviationAfr = d.AbbreviationAfr ?? "",
                });
            return list;
        }

        private List<Verse> LoadVerses()
        {
            VerseDto[] dtos = Read(VersesFile, SnapshotJsonContext.Default.VerseDtoArray);

            var list = new List<Verse>(dtos.Length);
            foreach (VerseDto d in dtos)
                list.Add(new Verse
                {
                    ID = d.Id,
                    BookNumber = d.BookId,

                    KjvChapter = d.KjvChapter,
                    KjvVerse = d.KjvVerse,
                    KjvSort = d.KjvSort,
                    KjvText = d.KjvText ?? "",

                    BsbChapter = d.BsbChapter,
                    BsbVerse = d.BsbVerse,
                    BsbSort = d.BsbSort,
                    BsbText = d.BsbText ?? "",

                    AovChapter = d.AovChapter,
                    AovVerse = d.AovVerse,
                    AovSort = d.AovSort,
                    AovText = d.AovText ?? "",
                });
            return list;
        }

        private List<CrossReference> LoadCrossReferences(HashSet<int> knownVerseIds)
        {
            CrossReferenceDto[] dtos = Read(CrossReferencesFile,
                                            SnapshotJsonContext.Default.CrossReferenceDtoArray);

            var list = new List<CrossReference>(dtos.Length);

            for (int i = 0; i < dtos.Length; i++)
            {
                CrossReferenceDto d = dtos[i];

                if (!knownVerseIds.Contains(d.VerseId))
                    continue;

                List<ReferenceRange> ranges = BuildRanges(d.Refs, knownVerseIds);
                if (ranges.Count == 0)
                    continue;

                list.Add(new CrossReference
                {
                    // The JSON carries no id of its own. The array position is
                    // stable for a given snapshot and makes a record easy to
                    // find in the file when something looks wrong.
                    ID = i,
                    SourceVerseID = d.VerseId,
                    SortOrder = d.Sort,
                    KjvAnchor = d.Kjv ?? "",
                    BsbAnchor = d.Bsb ?? "",
                    AovAnchor = d.Aov ?? "",
                    ReferenceRanges = ranges,
                });
            }

            return list;
        }

        private static List<ReferenceRange> BuildRanges(int[][]? refs, HashSet<int> knownVerseIds)
        {
            var ranges = new List<ReferenceRange>(refs?.Length ?? 0);
            if (refs is null)
                return ranges;

            foreach (int[] group in refs)
            {
                if (group is null || group.Length == 0)
                    continue;

                int[] ids = Array.TrueForAll(group, knownVerseIds.Contains)
                    ? group
                    : Array.FindAll(group, knownVerseIds.Contains);

                if (ids.Length > 0)
                    ranges.Add(new ReferenceRange { VerseIDs = ids });
            }

            return ranges;
        }

        private T[] Read<T>(string fileName, JsonTypeInfo<T[]> typeInfo)
        {
            string path = Path.Combine(_folder, fileName);

            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"Snapshot file not found: {path}. Expected {BooksFile}, {VersesFile} and " +
                    $"{CrossReferencesFile} in the configured data folder.", path);

            // Streaming rather than reading the whole file into a string first:
            // the verse file is large enough that the intermediate string would
            // land on the large object heap for no benefit.
            using var stream = new FileStream(path,
                                              FileMode.Open,
                                              FileAccess.Read,
                                              FileShare.Read,
                                              bufferSize: 64 * 1024,
                                              FileOptions.SequentialScan);

            return JsonSerializer.Deserialize(stream, typeInfo)
                   ?? throw new InvalidDataException($"{fileName} deserialised to null.");
        }
    }
}
