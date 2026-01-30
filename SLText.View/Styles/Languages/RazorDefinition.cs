using SkiaSharp;
using SLText.View.Styles.Languages.Helpers;

namespace SLText.View.Styles.Languages;

public class RazorDefinition : LanguageDefinition
{
    public override string Name => "Razor";
    public override string[] Extensions => [".razor", ".cshtml"];
    public override List<(string Pattern, Func<EditorTheme, SKColor> ColorSelector)> GetRules()
    {
        var rules = new List<(string Pattern, Func<EditorTheme, SKColor> ColorSelector)>
        {
            ( @"@\w+", theme => theme.Keyword ),                  
            ( @"(?<=<)/?[a-zA-Z0-9]+", theme => theme.Keyword ),   
            ( @"\b[a-zA-Z0-9-]+(?==)", theme => theme.Type ),     
            ( "\".*?\"", theme => theme.String ),
            ( @"@\{|@\(|@\)", theme => theme.Cursor )              
        };

        rules.AddRange(SharedRules.GetCssRules());
        
        rules.AddRange(SharedRules.GetJsRules());
        
        return rules;
    }
}