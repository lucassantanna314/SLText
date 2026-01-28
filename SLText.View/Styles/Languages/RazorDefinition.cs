using SkiaSharp;

namespace SLText.View.Styles.Languages;

public class RazorDefinition : LanguageDefinition
{
    public override string Name => "Razor";
    public override string[] Extensions => new[] { ".razor", ".cshtml" };
    public override List<(string Pattern, Func<EditorTheme, SKColor> ColorSelector)> GetRules() => new()
    {
        ( @"@\w+", theme => theme.Keyword ),                  // Diretivas (@code, @if)
        ( @"(?<=<)/?[a-zA-Z0-9]+", theme => theme.Keyword ),   // Tags HTML
        ( @"\b[a-zA-Z0-9-]+(?==)", theme => theme.Type ),      // Atributos HTML
        ( "\".*?\"", theme => theme.String ),
        ( @"@\{|@\(|@\)", theme => theme.Cursor )              // Blocos de código destacados
    };
}