using SkiaSharp;
using SLText.View.Styles.Languages.Helpers;

namespace SLText.View.Styles.Languages;

public class HtmlDefinition : LanguageDefinition
{
    public override string Name => "HTML";
    public override string[] Extensions => [".html", ".htm"];

    public override List<(string Pattern, Func<EditorTheme, SKColor> ColorSelector)> GetRules()
    {
        var rules = new List<(string Pattern, Func<EditorTheme, SKColor> ColorSelector)>
        {
            (@"", theme => theme.Comment),
            (@"(?<=<)/?[a-zA-Z0-9]+", theme => theme.Keyword),
            (@"\b[a-zA-Z0-9-]+(?==)", theme => theme.Type),
            ("\".*?\"", theme => theme.String),
            (@"[<>/=]", theme => theme.GutterForeground)
        };
        
        rules.AddRange(SharedRules.GetCssRules());
        
        rules.AddRange(SharedRules.GetJsRules());
        
        return rules;
    }
}