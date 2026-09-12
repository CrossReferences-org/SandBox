namespace SandBox.Services.ConnectionExplorer;

public record ConnectionExplorerResult(
    List<CohortVerse> Cohort,
    DisplayedVerse? Primary,
    IEnumerable<DisplayedVerse> ContextBefore,
    IEnumerable<DisplayedVerse> ContextAfter,
    List<RankedHit> Ranked,
    BibleTranslation Translation,
    bool Found,
    (int? Chapter, int? Chapter2, int? Verse, int? Verse2) PossiblyUpdatedRefs,
    Dictionary<int, double> bookHitScores);
