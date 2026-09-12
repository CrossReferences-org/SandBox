using SandBox.Data;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SandBox.Services.ConnectionExplorer
{
    public class DataCache
    {
        public IReadOnlyList<Book> Books { get; }
        public FrozenDictionary<int, Verse> VerseDict => _verseDict;
        public FrozenDictionary<int, Book> BookDict => _bookDict;
        public FrozenDictionary<int, List<CrossReference>> RefsOnSourceDict => _refsOnSourceDict;
        public FrozenDictionary<int, (int reffedByID, int reffedWithHowManyOtherVerses)[]> IncomingDict => _incomingDict;

        private readonly List<Verse> _verses;
        private readonly int[] _verseIDs;
        private readonly FrozenDictionary<int, Verse> _verseDict;
        private readonly FrozenDictionary<int, Book> _bookDict;
        private readonly FrozenDictionary<int, List<CrossReference>> _refsOnSourceDict;
        private readonly FrozenDictionary<int, (int reffedByID, int reffedWithHowManyOtherVerses)[]> _incomingDict;

        private readonly FrozenDictionary<(int, int, int), List<Verse>> _kjvIndex;
        private readonly FrozenDictionary<(int, int, int), List<Verse>> _bsbIndex;
        private readonly FrozenDictionary<(int, int, int), List<Verse>> _aovIndex;

        // A shared empty list to avoid allocating on misses
        private static readonly List<Verse> _emptyList = [];

        public readonly FrozenDictionary<int, int> KjvChapterCountsPerBook;
        public readonly FrozenDictionary<int, int> BsbChapterCountsPerBook;
        public readonly FrozenDictionary<int, int> AovChapterCountsPerBook;

        public readonly FrozenDictionary<(int BookNumber, int Chapter), int> KjvVerseCountsPerChapter;
        public readonly FrozenDictionary<(int BookNumber, int Chapter), int> BsbVerseCountsPerChapter;
        public readonly FrozenDictionary<(int BookNumber, int Chapter), int> AovVerseCountsPerChapter;

        public DataCache(string jsonPath)
        {
            Stopwatch total = Stopwatch.StartNew();
            Stopwatch step = Stopwatch.StartNew();

            void Step(string label)
            {
                Console.WriteLine($"  {label,-34}{step.ElapsedMilliseconds,6} ms");
                step.Restart();
            }

            Console.WriteLine($"Loading JSON... ({jsonPath})");

            SnapshotLoader loader = new(jsonPath);
            (List<Book> books, List<Verse> verses, List<CrossReference> xrefs) = loader.LoadAll();
            Step("Read and parse files");

            Books = books!.OrderBy(b => b.ID).ToList();
            _verses = verses.OrderBy(x => x.ID).ToList();
            _verseIDs = verses.Select(x => x.ID).Order().ToArray();
            Step("Sort books and verses");

            _verseDict = _verses.ToFrozenDictionary(v => v.ID);
            _bookDict = Books.ToFrozenDictionary(b => b.ID);
            Step("Freeze verse and book lookups");

            _refsOnSourceDict = xrefs!
                .GroupBy(x => x.SourceVerseID)
                .ToFrozenDictionary(g => g.Key, g => g.OrderBy(x => x.SortOrder).ToList());
            Step("Group references by source");

            // For each verse, who cites it and how many verses they cited it
            // alongside. Written as a loop rather than SelectMany + GroupBy:
            // the LINQ version allocates a tuple per target ID and a grouping
            // per verse, which dominated load time.
            var incoming = new Dictionary<int, List<(int reffedByID, int reffedWithHowManyOtherVerses)>>(_verses.Count);

            foreach (CrossReference xRef in xrefs)
                foreach (ReferenceRange range in xRef.ReferenceRanges)
                {
                    int rangeLength = range.VerseIDs.Length;

                    foreach (int targetId in range.VerseIDs)
                    {
                        ref List<(int, int)>? citers =
                            ref CollectionsMarshal.GetValueRefOrAddDefault(incoming, targetId, out _);

                        citers ??= new List<(int, int)>(4);
                        citers.Add((xRef.SourceVerseID, rangeLength));
                    }
                }

            _incomingDict = incoming.ToFrozenDictionary(kv => kv.Key, kv => kv.Value.ToArray());
            Step("Build incoming reference map");

            _kjvIndex = BuildIndex(_verses,
                                   v => (v.BookNumber, v.KjvChapter, v.KjvVerse),
                                   (a, b) => a.KjvSort.CompareTo(b.KjvSort));

            _bsbIndex = BuildIndex(_verses,
                                   v => (v.BookNumber, v.BsbChapter, v.BsbVerse),
                                   (a, b) => a.BsbSort.CompareTo(b.BsbSort));

            _aovIndex = BuildIndex(_verses,
                                   v => (v.BookNumber, v.AovChapter, v.AovVerse),
                                   (a, b) => a.AovSort.CompareTo(b.AovSort));
            Step("Build per-translation indexes");

            //KjvVerseChapterPerBook
            KjvChapterCountsPerBook = _verses.GroupBy(x => x.BookNumber).Select(x => (BookNr: x.Key, ChapterCount: x.DistinctBy(c => c.KjvChapter).Count())).ToFrozenDictionary(x => x.BookNr, x => x.ChapterCount);
            BsbChapterCountsPerBook = _verses.GroupBy(x => x.BookNumber).Select(x => (BookNr: x.Key, ChapterCount: x.DistinctBy(c => c.BsbChapter).Count())).ToFrozenDictionary(x => x.BookNr, x => x.ChapterCount);
            AovChapterCountsPerBook = _verses.GroupBy(x => x.BookNumber).Select(x => (BookNr: x.Key, ChapterCount: x.DistinctBy(c => c.AovChapter).Count())).ToFrozenDictionary(x => x.BookNr, x => x.ChapterCount);
            Step("Count chapters per book");

            KjvVerseCountsPerChapter = _verses.DistinctBy(x => (x.BookNumber, x.KjvChapter, x.KjvVerse)).CountBy(x => (x.BookNumber, x.KjvChapter)).ToFrozenDictionary(x => x.Key, x => x.Value);
            BsbVerseCountsPerChapter = _verses.DistinctBy(x => (x.BookNumber, x.BsbChapter, x.BsbVerse)).CountBy(x => (x.BookNumber, x.BsbChapter)).ToFrozenDictionary(x => x.Key, x => x.Value);
            AovVerseCountsPerChapter = _verses.DistinctBy(x => (x.BookNumber, x.AovChapter, x.AovVerse)).CountBy(x => (x.BookNumber, x.AovChapter)).ToFrozenDictionary(x => x.Key, x => x.Value);
            Step("Count verses per chapter");

            total.Stop();
            Console.WriteLine($"JSON loaded. {Books.Count} books, {_verses.Count} verses, " +
                              $"{xrefs.Count} cross-references in {total.ElapsedMilliseconds} ms.");
        }

        // _verses and _verseIDs are both ordered by Verse.ID, so index i in one
        // addresses the same verse as index i in the other. The three methods below
        // depend on that; keep them in sync if either is ever rebuilt.

        /// <summary>Position of a verse in canonical order, or -1 if not present.</summary>
        public int IndexOf(int verseId)
        {
            int i = Array.BinarySearch(_verseIDs, verseId);
            return i < 0 ? -1 : i;
        }

        public int VerseCount => _verses.Count;

        public Verse VerseAt(int index) => _verses[index];

        /// <summary>All verses from fromId to toId inclusive, in canonical order.</summary>
        public ReadOnlySpan<Verse> VerseRange(int fromId, int toId)
        {
            int from = IndexOf(fromId);
            int to = IndexOf(toId);
            return from < 0 || to < 0 || to < from
                ? []
                : CollectionsMarshal.AsSpan(_verses).Slice(from, to - from + 1);
        }

        /// <summary>
        /// Groups verses by (book, chapter, verse) for one translation, ordering
        /// each group by that translation's sort field. Almost every group holds
        /// a single record — only split verses have more — so the sort is skipped
        /// unless it can actually do something.
        /// </summary>
        private static FrozenDictionary<(int, int, int), List<Verse>> BuildIndex(
            List<Verse> verses,
            Func<Verse, (int, int, int)> keySelector,
            Comparison<Verse> bySortField)
        {
            var map = new Dictionary<(int, int, int), List<Verse>>(verses.Count);

            foreach (Verse v in verses)
            {
                ref List<Verse>? group =
                    ref CollectionsMarshal.GetValueRefOrAddDefault(map, keySelector(v), out _);

                group ??= new List<Verse>(1);
                group.Add(v);
            }

            foreach (List<Verse> group in map.Values)
                if (group.Count > 1)
                    group.Sort(bySortField);

            return map.ToFrozenDictionary();
        }

        public List<Verse> ResolveByIndex(int bookId, int chapter, int verseNum, BibleTranslation translation)
        {
            var key = (bookId, chapter, verseNum);

            var targetIndex = translation switch
            {
                BibleTranslation.KJV => _kjvIndex,
                BibleTranslation.BSB => _bsbIndex,
                BibleTranslation.AOV => _aovIndex,
                _ => _kjvIndex,
            };

            return targetIndex.TryGetValue(key, out var result) ? result : _emptyList;
        }

        internal int GetChapterCount(BibleTranslation translation, int bookId) => translation switch
        {
            BibleTranslation.KJV => KjvChapterCountsPerBook.GetValueOrDefault(bookId),
            BibleTranslation.BSB => BsbChapterCountsPerBook.GetValueOrDefault(bookId),
            BibleTranslation.AOV => AovChapterCountsPerBook.GetValueOrDefault(bookId),
            _ => 1,
        };

        internal int GetVerseCount(BibleTranslation translation, int bookId, int chapter) => translation switch
        {
            BibleTranslation.KJV => KjvVerseCountsPerChapter.GetValueOrDefault((bookId, chapter)),
            BibleTranslation.BSB => BsbVerseCountsPerChapter.GetValueOrDefault((bookId, chapter)),
            BibleTranslation.AOV => AovVerseCountsPerChapter.GetValueOrDefault((bookId, chapter)),
            _ => 1,
        };
    }
}