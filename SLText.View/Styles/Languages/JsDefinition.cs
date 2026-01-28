using SkiaSharp;

namespace SLText.View.Styles.Languages;

public class JsDefinition : LanguageDefinition
{
    public override string Name => "JavaScript";
    public override string[] Extensions => new[] { ".js" };
    public override List<(string Pattern, Func<EditorTheme, SKColor> ColorSelector)> GetRules() => new()
    {
        ( @"//.*", theme => theme.Comment ),
        ( @"/\*.*?\*/", theme => theme.Comment ),
        ( "\".*?\"|'.*?'", theme => theme.String ),
        ( @"\b(const|let|var|function|return|if|else|for|while|import|export|from|class|new|this)\b", theme => theme.Keyword ),
        ( @"\b(true|false|null|undefined)\b", theme => theme.Number ),
        ( @"\b\w+(?=\s*\()", theme => theme.Method )
    };
}