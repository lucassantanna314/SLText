using SkiaSharp;

namespace SLText.View.Styles.Languages;

public abstract class LanguageDefinition
{
    public abstract string Name { get; }
    public abstract string[] Extensions { get; }
    public abstract List<(string Pattern, Func<EditorTheme, SKColor> ColorSelector)> GetRules();
}