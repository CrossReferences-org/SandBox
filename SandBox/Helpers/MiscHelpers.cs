using SandBox.Data;
using SandBox.Services.ConnectionExplorer;

namespace SandBox.Helpers;

public static class MiscHelpers
{
    public static bool TryPrepareKeyDirectory(string? path, out DirectoryInfo? dir)
    {
        dir = null;

        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            Directory.CreateDirectory(path);

            // Existing isn't the same as writable — root-owned directories pass
            // the first check and fail at the first write.
            string probe = Path.Combine(path, $".probe-{Guid.NewGuid():N}");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);

            dir = new DirectoryInfo(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Key persistence disabled ({path}): {ex.Message}");
            return false;
        }
    }

    public static (int ch, int vs, string text) GetDisplay(Verse v, BibleTranslation translation) => translation switch
    {
        BibleTranslation.KJV => (v.KjvChapter, v.KjvVerse, v.KjvText),
        BibleTranslation.BSB => (v.BsbChapter, v.BsbVerse, v.BsbText),
        BibleTranslation.AOV => (v.AovChapter, v.AovVerse, v.AovText),
        _ => (v.KjvChapter, v.KjvVerse, v.KjvText),
    };

    public static string GetBookName(Book b, BibleTranslation translation) => translation switch
    {
        BibleTranslation.KJV or BibleTranslation.BSB => b.NameEng,
        BibleTranslation.AOV => b.NameAfr,
        _ => b.NameEng,
    };

    public static string? GetAnchor(CrossReference x, BibleTranslation translation) => translation switch
    {
        BibleTranslation.KJV => x.KjvAnchor,
        BibleTranslation.BSB => x.BsbAnchor,
        BibleTranslation.AOV => x.AovAnchor,
        _ => string.Empty,
    };


    public static IEnumerable<RankedHit> GatherContext(RankedHit[] allUnCut_Ordered,
                                                                RankedHit primary,
                                                                int ri,
                                                                HashSet<RankedHit> used)
    {
        List<RankedHit> precedingVerses = [];
        int lastID = primary.Verse.Id;
        double primaryScore = primary.Score;

        int distance = 1;
        for (int i = ri - 1; i >= 0; i--)
        {
            double cutoff = ContextCutOff(primaryScore, distance);

            RankedHit p = allUnCut_Ordered[i];
            if (lastID - p.Verse.Id <= 10 &&
                p.Score >= cutoff &&
                p.Verse.BookId == primary.Verse.BookId &&
                (p.Verse.BookId != 19 || p.Verse.Chapter == primary.Verse.Chapter) &&
                !used.Contains(p))
            {
                precedingVerses.Add(p);
                used.Add(p);
                lastID = p.Verse.Id;
            }
            else
                break;

            distance += 1;
        }
        precedingVerses.Reverse();

        List<RankedHit> followingVerses = [];
        lastID = primary.Verse.Id;

        distance = 1;
        for (int i = ri + 1; i < allUnCut_Ordered.Length; i++)
        {
            double cutoff = ContextCutOff(primaryScore, distance);

            RankedHit p = allUnCut_Ordered[i];
            if (p.Verse.Id - lastID <= 10 &&
                p.Score >= cutoff &&
                p.Verse.BookId == primary.Verse.BookId &&
               (p.Verse.BookId != 19 || p.Verse.Chapter == primary.Verse.Chapter) &&
                !used.Contains(p))
            {
                followingVerses.Add(p);
                used.Add(p);
                lastID = p.Verse.Id;
            }
            else
                break;

            distance += 1;
        }

        return precedingVerses.Append(primary).Concat(followingVerses);
    }
    private static double ContextCutOff(double primaryScore, int distance)
        => primaryScore * Math.Min(1, 0.16 * Math.Pow(distance, 0.79));
}
