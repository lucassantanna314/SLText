using SkiaSharp;

namespace SLText.View.Styles.Languages;

public class XmlDefinition : LanguageDefinition
{
    public override string Name => "XML/Project";
    public override string[] Extensions => new[] { ".xml", ".csproj", ".props" };
    public override List<(string Pattern, Func<EditorTheme, SKColor> ColorSelector)> GetRules() => new()
    {
        ( @"", theme => theme.Comment ),
        ( @"<[a-zA-Z0-9]+", theme => theme.Keyword ), 
        ( @"[a-zA-Z0-9]+=", theme => theme.Type ),    
        ( "\".*?\"", theme => theme.String )
    };
}