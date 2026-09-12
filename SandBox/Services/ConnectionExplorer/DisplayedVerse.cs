namespace SandBox.Services.ConnectionExplorer;

public record DisplayedVerse(
   int Id,
   int BookId,
   string BookName,
   int Chapter,
   int Verse,
   string Text);