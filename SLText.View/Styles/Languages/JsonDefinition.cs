using SkiaSharp;

namespace SLText.View.Styles.Languages;

public class JsonDefinition : LanguageDefinition
{
    public override string Name => "JSON";
    public override string[] Extensions => new[] { ".json" };
    public override List<(string Pattern, Func<EditorTheme, SKColor> ColorSelector)> GetRules() => new()
    {
        ( "\".*?\"(?=\\s*:)", theme => theme.Type ),    
        ( "(?<=:\\s*)\".*?\"", theme => theme.String ),  
        ( @"\b(true|false|null)\b", theme => theme.Keyword ),
        ( @"\b\d+\b", theme => theme.Number )
    };
}