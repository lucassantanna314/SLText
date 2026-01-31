using SkiaSharp;
using SLText.View.Styles.Languages.Helpers;

namespace SLText.View.Styles.Languages;

public class JsDefinition : LanguageDefinition
{
    public override string Name => "JavaScript";
    public override string[] Extensions => new[] { ".js" };
    public override List<(string Pattern, Func<EditorTheme, SKColor> ColorSelector)> GetRules() 
        => SharedRules.GetJsRules(); 
}