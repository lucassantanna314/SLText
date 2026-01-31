using SkiaSharp;
using SLText.View.Styles.Languages.Helpers;

namespace SLText.View.Styles.Languages;

public class CssDefinition : LanguageDefinition
{
    public override string Name => "CSS";
    public override string[] Extensions => [".css"];
    
    public override List<(string Pattern, Func<EditorTheme, SKColor> ColorSelector)> GetRules() 
        => SharedRules.GetCssRules(); 
}