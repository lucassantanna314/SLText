using SkiaSharp;

namespace SLText.View.Styles.Languages;

public class HtmlDefinition : LanguageDefinition
{
    public override string Name => "HTML";
    public override string[] Extensions => new[] { ".html", ".htm" };
    public override List<(string Pattern, Func<EditorTheme, SKColor> ColorSelector)> GetRules() => new()
    {
        ( @"", theme => theme.Comment ),
        ( @"(?<=<)/?[a-zA-Z0-9]+", theme => theme.Keyword ), // Tags
        ( @"\b[a-zA-Z0-9-]+(?==)", theme => theme.Type ),    // Atributos
        ( "\".*?\"", theme => theme.String ),
        ( @"[<>/=]", theme => theme.GutterForeground )       // Símbolos sutis
    };
}