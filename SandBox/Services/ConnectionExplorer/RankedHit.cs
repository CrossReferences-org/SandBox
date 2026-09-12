namespace SandBox.Services.ConnectionExplorer;

public record RankedHit(
    DisplayedVerse Verse,
    CountInfo CountInfo,
    double Score);
