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
    
    public static List<(string Pattern, Func<EditorTheme, SKColor> ColorSelector)> GetJsRules() => new()
    {
        ( @"//.*", theme => theme.Comment ),
        ( @"/\*.*?\*/", theme => theme.Comment ),
        ( "\".*?\"|'.*?'", theme => theme.String ),
        ( @"\b(const|let|var|function|return|if|else|for|while|import|export|from|class|new|this)\b", theme => theme.Keyword ),
        ( @"\b(true|false|null|undefined)\b", theme => theme.Number ),
        ( @"\b\w+(?=\s*\()", theme => theme.Method )
    };
}