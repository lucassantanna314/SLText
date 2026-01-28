using SkiaSharp;

namespace SLText.View.Styles.Languages;

public class CssDefinition : LanguageDefinition
{
    public override string Name => "CSS";
    public override string[] Extensions => new[] { ".css" };
    public override List<(string Pattern, Func<EditorTheme, SKColor> ColorSelector)> GetRules() => new()
    {
        ( @"/\*.*?\*/", theme => theme.Comment ),
        ( @"[:;{}]", theme => theme.GutterForeground ),
        ( @"\b[a-zA-Z-]+\b(?=\s*:)", theme => theme.Keyword ), // Propriedades
        ( @"(?<=:\s*)[^;{}]+", theme => theme.String ),        // Valores
        ( @"[.#][a-zA-Z0-9_-]+", theme => theme.Method )      // Classes e IDs
    };
}