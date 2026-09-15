using SandBox.Data;
using SandBox.Helpers;
using System.Collections.Frozen;
using System.Runtime.InteropServices;

namespace SandBox.Services.ConnectionExplorer;

public class ConnectionExplorerService
{
    private readonly DataCache _dataCache;

    private FrozenDictionary<int, Verse> _verseDict => _dataCache.VerseDict;
    private FrozenDictionary<int, Book> _bookDict => _dataCache.BookDict;
    private FrozenDictionary<int, List<CrossReference>> _refsOnSourceDict => _dataCache.RefsOnSourceDict;
    private FrozenDictionary<int, (int reffedByID, int reffedWithHowManyOtherVerses)[]> _incomingDict => _dataCache.IncomingDict;
    public IReadOnlyList<Book> Books => _dataCache.Books;

    public ConnectionExplorerService(DataCache dataCache)
    {
        _dataCache = dataCache;
    }

    public ConnectionExplorerResult GetConnections(int? bookId,
                                                    int? chapter,
                                                    int? chapter2,
                                                    int? verse,
                                                    int? verse2,
                                                    int? id,
                                                    BibleTranslation? tr,
                                                    string? anchorPhrase,
                                                    int[]? batchIds,
                                                    bool? executeIDs,
                                                    int[]? filterBooks)
    {
        BibleTranslation translation = tr ?? BibleTranslation.KJV;

        // We only correct these inputs if not using ids.
        // Because CorrectInputs operates on this assumption.
        if (!id.HasValue && !(batchIds?.Length > 0 && executeIDs == true))
            CorrectInputs(ref bookId,
                          ref chapter,
                          ref chapter2,
                          ref verse,
                          ref verse2,
                          translation);

        List<Verse> sources = ResolveSources(
            bookId, chapter, chapter2, verse, verse2, id, batchIds, executeIDs, translation);

        if (sources.Count == 0)
        {
            return new ConnectionExplorerResult(
                Cohort: [],
                Primary: null,
                ContextBefore: [],
                ContextAfter: [],
                Ranked: [],
                Translation: translation,
                Found: false,
                PossiblyUpdatedRefs: (chapter, chapter2, verse, verse2),
                []);
        }

        List<CohortVerse> cohort = GetCohortVerses(translation, sources);
        IEnumerable<DisplayedVerse> contextBefore = GetContextBefore(_verseDict[cohort[0].Verse.Id], 2).Select(x => ToDisplayed(x, translation));
        IEnumerable<DisplayedVerse> contextAfter = GetContextAfter(_verseDict[cohort[^1].Verse.Id], 2).Select(x => ToDisplayed(x, translation));

        //var sw = System.Diagnostics.Stopwatch.StartNew();
        int depth = 3;
        Dictionary<int, CountInfo> hopResult = SandBoxHop(sources,
                                                         anchorPhrase,
                                                         translation,
                                                         depth,
                                                         executeIDs);
        //sw.Stop();
        //Console.WriteLine($"SandBoxHop: {sw.Elapsed.TotalMilliseconds:F1} ms");

        List<RankedHit> ranked = GetRankedHits(translation,
                                               hopResult,
                                               filterBooks,
                                               out Dictionary<int, double> bookHitScores);

        return new ConnectionExplorerResult(
            Cohort: cohort,
            Primary: cohort[0].Verse,
            ContextBefore: contextBefore,
            ContextAfter: contextAfter,
            Ranked: ranked,
            Translation: translation,
            Found: true,
            PossiblyUpdatedRefs: (chapter, chapter2, verse, verse2),
            bookHitScores: bookHitScores);
    }

    private readonly double _cutoff = new CountInfo(L1: 0,
                                                    L2: 2,
                                                    L3: 0,
                                                    L1_Incoming: 0,
                                                    L1_Incoming_L1_Overlap: 0).WeightedScore();
    private List<RankedHit> GetRankedHits(BibleTranslation translation,
                                          Dictionary<int, CountInfo> hopResult,
                                          int[]? filterBooks,
                                          out Dictionary<int, double> bookHitScores)
    {

        var ranked = hopResult.Select(kv => (kv, score: kv.Value.WeightedScore()))
                              .Where(x => x.score >= _cutoff)
                              .OrderByDescending(x => x.score)
                              .Select(x => new RankedHit(Verse: ToDisplayed(_verseDict[x.kv.Key], translation),
                                       CountInfo: x.kv.Value,
                                       Score: x.score))
                              .ToList();

        // We do this before we filter out the books, because we want the stats for the entire set.
        bookHitScores = ranked.Select(x => (_dataCache.VerseDict[x.Verse.Id].BookNumber, x.Score))
                              .GroupBy(x => x.BookNumber)
                              .ToDictionary(x => x.Key, x => x.Max(c => c.Score));

        return filterBooks?.Length > 0
            ? ranked.Where(x => filterBooks.Contains(x.Verse.BookId)).ToList()
            : ranked;
    }

    private List<CohortVerse> GetCohortVerses(BibleTranslation translation,
                                              List<Verse> sources)
        => sources.Select(v =>
           {
               List<string> anchorList = (_refsOnSourceDict.TryGetValue(v.ID, out var refs)
                                         ? refs.Select(x => MiscHelpers.GetAnchor(x, translation))
                                               .Where(a => !string.IsNullOrWhiteSpace(a))
                                               .Select(a => a!)
                                         : [])
                                         .ToList();

               IReadOnlyList<string> anchors = translation == BibleTranslation.KJV
                   ? anchorList                       // sequential: order matters, dups allowed
                   : anchorList.Distinct().ToList();  // others: phrase uniqueness is enough

               return new CohortVerse(ToDisplayed(v, translation), anchors);
           }).ToList();

    /// <summary>
    /// Scores every verse reachable from <paramref name="sources"/> through the
    /// cross-reference graph and returns a per-verse <see cref="CountInfo"/> of the
    /// evidence collected. Source verses themselves are excluded from the result.
    /// </summary>
    private Dictionary<int, CountInfo> SandBoxHop(List<Verse> sources,
                                             string? anchorPhrase,
                                             BibleTranslation translation,
                                             int depth,
                                             bool? executeIDs)
    {
        //HashSet<int> sourceIds = [.. sources.Select(v => v.ID)];
        var sourceIds = sources.Select(v => v.ID).ToFrozenSet();
        Dictionary<int, CountInfo> output = [];

        bool shouldReferenceFilter = !string.IsNullOrWhiteSpace(anchorPhrase) && !(executeIDs == true);
        Func<CrossReference, bool>? referenceFilter = shouldReferenceFilter
            ? (x => MiscHelpers.GetAnchor(x, translation) == anchorPhrase)
            : null;

        HashSet<int> L1_References = [];
        List<int> nextIDsToCheck = new(Convert.ToInt32(Math.Round(CountInfo.L2_Avg * 1.2)));

        // ---------- Hop 0 ----------
        // Anchor filter applies. Per-source dedup via L1Targets.
        // Side effect: build L1_References for the incoming-refs pass below.
        foreach (int srcId in sourceIds)
        {
            if (!_refsOnSourceDict.TryGetValue(srcId, out List<CrossReference>? refs))
                continue;

            HashSet<int> L1Targets = []; // Duplicate targets for a given source verse is imho a data smell, so we discard them.
            IEnumerable<CrossReference> xes = shouldReferenceFilter ? refs.Where(referenceFilter!) : refs;

            foreach (CrossReference x in xes)
                foreach (ReferenceRange range in x.ReferenceRanges)
                    foreach (int targetId in range.VerseIDs)
                    {
                        if (!L1Targets.Add(targetId))
                            continue;

                        if (depth > 1)
                            nextIDsToCheck.Add(targetId);

                        L1_References.Add(targetId);
                        ref CountInfo c = ref CollectionsMarshal.GetValueRefOrAddDefault(output, targetId, out _);
                        c.L1 += 1;
                    }
        }

        // ---------- Hops 1..depth-1 ----------
        // Each level is carried as distinct ids + how many paths reached each one,
        // instead of a list with repeats. Adding the count once is identical to
        // adding 1 that many times, but walks each verse's references only once.
        Dictionary<int, int> idsToCheckWithPathCount = new(nextIDsToCheck.Count);
        foreach (int targetId in nextIDsToCheck)
            idsToCheckWithPathCount[targetId] = 1;   // hop 0 deduped per source, so every count is 1 here

        for (int hop = 1; hop < depth; hop++)
        {
            bool isL2 = hop == 1;
            bool moreHops = hop < depth - 1;

            Dictionary<int, int> nextIDsToCheckWithPathCount = moreHops
                ? new(Convert.ToInt32(Math.Round(idsToCheckWithPathCount.Count * CountInfo.L1_Avg * 1.2)))
                : [];

            foreach ((int srcId, int pathCount) in idsToCheckWithPathCount)
            {
                if (!_refsOnSourceDict.TryGetValue(srcId, out List<CrossReference>? refs))
                    continue;

                foreach (CrossReference x in refs)
                    foreach (ReferenceRange range in x.ReferenceRanges)
                        foreach (int targetId in range.VerseIDs)
                        {
                            if (moreHops)
                            {
                                ref int targetPathCount = ref CollectionsMarshal.GetValueRefOrAddDefault(nextIDsToCheckWithPathCount, targetId, out _);
                                targetPathCount += pathCount;
                            }

                            ref CountInfo c = ref CollectionsMarshal.GetValueRefOrAddDefault(output, targetId, out _);

                            if (isL2)
                                c.L2 += pathCount;
                            else
                                c.L3 += pathCount;
                        }
            }

            idsToCheckWithPathCount = nextIDsToCheckWithPathCount;
        }

        // ---------- Incoming refs ----------
        HashSet<int> reffedByIdSeen = [];
        foreach (Verse v in sources)
        {
            if (!_incomingDict.TryGetValue(v.ID, out (int reffedByID, int ReffedWithHowManyOtherVerses)[]? inc))
                continue;

            int anchorCount = _refsOnSourceDict.TryGetValue(v.ID, out List<CrossReference>? refs) ? refs.Count : 1;

            foreach ((int reffedByID, int reffedWithHowManyOtherVerses) in inc)
            {
                if (sourceIds.Contains(reffedByID))
                    continue;

                if (!reffedByIdSeen.Add(reffedByID))
                    continue;

                ref CountInfo c = ref CollectionsMarshal.GetValueRefOrAddDefault(output, reffedByID, out _);

                // Spread the incoming weight across the source's anchor phrases.
                // Without an anchor filter we can't know which phrase the citation was aimed at,
                // so we treat them as equally likely.
                double scoreIncrement = 1.0
                    / reffedWithHowManyOtherVerses
                    / anchorCount;

                c.L1_Incoming += scoreIncrement;

                if (_refsOnSourceDict.TryGetValue(reffedByID,
                                                  out List<CrossReference>? reffedByID_refs))
                {
                    // If we have an incoming L1 that also links to the L1 references of our source verse
                    foreach (CrossReference x in reffedByID_refs)
                        foreach (ReferenceRange range in x.ReferenceRanges)
                            foreach (int targetId in range.VerseIDs)
                            {
                                if (L1_References.Contains(targetId))
                                    c.L1_Incoming_L1_Overlap += 1.0 / CountInfo.L1_to_L2;
                            }
                }
            }
        }

        return output.Where(c => !sourceIds.Contains(c.Key)).ToDictionary();
    }


    private void CorrectInputs(ref int? bookId,
                               ref int? chapter,
                               ref int? chapter2,
                               ref int? verse,
                               ref int? verse2,
                               BibleTranslation translation)
    {
        if (!bookId.HasValue)
            bookId = 1;

        if (!chapter.HasValue)
            chapter = 1;

        // Book
        {
            bookId = Math.Clamp(bookId.Value, min: 1, max: 66);
        }

        // Chapter
        {
            int maxChapterNr = _dataCache.GetChapterCount(translation, bookId.Value);
            chapter = Math.Clamp(chapter.Value, min: 1, max: maxChapterNr);

            if (chapter >= chapter2)
                chapter2 = null;

            if (chapter2.HasValue)
            {
                chapter2 = Math.Clamp(chapter2.Value, min: chapter.Value, max: maxChapterNr);
                int? maxVerseNrChapter2 = chapter2.HasValue ? _dataCache.GetVerseCount(translation, bookId.Value, chapter2.Value) : null;
                if (!verse2.HasValue || verse2 <= 0)
                    verse2 = maxVerseNrChapter2;
            }
        }

        // Verse and Verse2
        {
            int maxVerseNr = _dataCache.GetVerseCount(translation, bookId.Value, chapter.Value);
            int? maxVerseNrChapter2 = chapter2.HasValue ? _dataCache.GetVerseCount(translation, bookId.Value, chapter2.Value) : null;

            // A chapter is given, but no verses. So we take the entire chapter.
            if (!verse.HasValue && !verse2.HasValue)
            {
                verse = 1;

                if (chapter2.HasValue)
                    verse2 = maxVerseNrChapter2;
                else
                    verse2 = maxVerseNr;

                return;
            }

            if (verse.HasValue)
                verse = Math.Clamp(verse.Value, min: 1, max: maxVerseNr);
            else
                verse = 1;

            if (verse2.HasValue)
            {
                if (chapter2.HasValue)
                {
                    verse2 = Math.Clamp(verse2.Value, min: 1, max: maxVerseNrChapter2!.Value);
                }
                else
                {
                    if (verse2 <= verse)
                        verse2 = null;
                    else
                        verse2 = Math.Clamp(verse2.Value, min: verse.Value, max: maxVerseNr);
                }
            }
        }
    }

    private List<Verse> ResolveSources(int? bookId,
                                         int? chapter,
                                         int? chapter2,
                                         int? verse,
                                         int? verse2,
                                         int? id,
                                         int[]? batchIds,
                                         bool? executeIDs,
                                         BibleTranslation translation)
    {
        // Batch ID job
        if (executeIDs == true &&
            batchIds?.Length > 0)
        {
            List<Verse> sources = [];

            foreach (int bid in batchIds.Distinct())
            {
                if (_verseDict.TryGetValue(bid, out Verse? v))
                    sources.Add(v);
            }

            return sources;
        }

        // Single ID Job
        if (id.HasValue && _verseDict.TryGetValue(id.Value, out Verse? seed))
            return ExpandToCohort(seed, translation);

        // Reference Job (Book, Chapter, Verse, [Verse2])
        if (bookId.HasValue &&
            chapter.HasValue &&
            verse.HasValue)
        {
            // MultiChapter Job
            if (chapter2.HasValue && verse2.HasValue)
            {
                List<Verse> first = _dataCache.ResolveByIndex(bookId.Value, chapter.Value, verse.Value, translation);
                List<Verse> last = _dataCache.ResolveByIndex(bookId.Value, chapter2.Value, verse2.Value, translation);

                if (first.Count == 0 || last.Count == 0)
                    return [];

                return [.. _dataCache.VerseRange(first.Min(v => v.ID), last.Max(v => v.ID))];
            }

            // Verse Range Job
            if (verse2.HasValue && verse2.Value > verse.Value)
                return Enumerable.Range(verse.Value, verse2.Value - verse.Value + 1)
                                 .SelectMany(v => _dataCache.ResolveByIndex(bookId.Value, chapter.Value, v, translation))
                                 .ToList();

            // Single Verse Job
            return _dataCache.ResolveByIndex(bookId.Value, chapter.Value, verse.Value, translation);
        }

        return [];
    }

    private IEnumerable<Verse> GetContextBefore(Verse verse,
                                                  int count)
    {
        int vi = _dataCache.IndexOf(verse.ID);
        if (vi < 0) yield break;

        int start = Math.Max(0, vi - count);
        for (int i = start; i < vi; i++)
        {
            Verse v = _dataCache.VerseAt(i);
            if (v.BookNumber != verse.BookNumber) continue;
            yield return v;
        }
    }

    private IEnumerable<Verse> GetContextAfter(Verse verse,
                                                 int count)
    {
        int vi = _dataCache.IndexOf(verse.ID);
        if (vi < 0) yield break;

        int end = Math.Min(_dataCache.VerseCount - 1, vi + count);
        for (int i = vi + 1; i <= end; i++)
        {
            Verse v = _dataCache.VerseAt(i);
            if (v.BookNumber != verse.BookNumber) break;
            yield return v;
        }
    }


    private DisplayedVerse ToDisplayed(Verse v,
                                       BibleTranslation translation)
    {
        (int ch, int vs, string? text) = MiscHelpers.GetDisplay(v, translation);
        string bookName = _bookDict.TryGetValue(v.BookNumber, out Book? b)
                          ? MiscHelpers.GetBookName(b, translation)
                          : "?";

        return new DisplayedVerse(v.ID, v.BookNumber, bookName, ch, vs, text);
    }

    private List<Verse> ExpandToCohort(Verse seed,
                                         BibleTranslation translation)
    {
        (int ch, int vs, string _) = MiscHelpers.GetDisplay(seed, translation);
        return _dataCache.ResolveByIndex(seed.BookNumber, ch, vs, translation);
    }

    public IEnumerable<DisplayedVerse> GetDisplayVersesFromIds(IEnumerable<int> ids,
                                                               BibleTranslation translation)
    {
        foreach (int id in ids)
        {
            if (_verseDict.TryGetValue(id, out Verse? v))
                yield return ToDisplayed(v, translation);
        }
    }

    #region stats
    /*
=== L1 ===
n=31217  avg=19.14  p50=15  p90=39  p99=82  max=190
zeros: 242 (0.8%)
[    0..    1):    120
[    1..    2):    361
[    2..    5):   2333  ##
[    5..   10):   6686  ########
[   10..   25):  13841  #################
[   25..   50):   6166  #######
[   50..  100):   1597  ##
[  100..  250):    113
=== L2 ===
n=31217  avg=497.31  p50=304  p90=1146  p99=2881  max=7653
zeros: 127 (0.4%)
[    0..    1):    123
[    1..    2):      8
[    2..    5):     35
[    5..   10):     97
[   10..   25):    570
[   25..   50):   1266  #
[   50..  100):   3078  ###
[  100..  250):   8220  ##########
[  250..  500):   7705  #########
[  500.. 1000):   6166  #######
[ 1000.. 5000):   3932  #####
[ 5000+    ]:     17
=== L3 ===
n=31217  avg=14076.70  p50=7406  p90=34241  p99=96729  max=269765
zeros: 123 (0.4%)
[    0..    1):    123
[    2..    5):      1
[    5..   10):      2
[   10..   25):     13
[   25..   50):     26
[   50..  100):     52
[  100..  250):    240
[  250..  500):    623
[  500.. 1000):   1442  #
[ 1000.. 5000):   9432  ############
[ 5000+    ]:  19263  ########################
*/
    #endregion
}
