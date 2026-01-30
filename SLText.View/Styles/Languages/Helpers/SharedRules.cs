using SkiaSharp;

namespace SLText.View.Styles.Languages.Helpers;

public static class SharedRules
{
    public static List<(string Pattern, Func<EditorTheme, SKColor> ColorSelector)> GetCssRules() => new()
    {
        ( @"/\*.*?\*/", theme => theme.Comment ),
        ( @"\b[a-zA-Z-]+\b(?=\s*:)", theme => theme.Keyword ),
        ( @"(?<=:\s*)[^;{}]+", theme => theme.String ),        
        ( @"[.#][a-zA-Z0-9_-]+", theme => theme.Method )      
    };
}