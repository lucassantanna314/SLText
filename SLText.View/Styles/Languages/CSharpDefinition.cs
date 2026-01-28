using SkiaSharp;

namespace SLText.View.Styles.Languages;

public class CSharpDefinition : LanguageDefinition
{
    public override string Name => "C#";
    public override string[] Extensions => new[] { ".cs" };

    public override List<(string, Func<EditorTheme, SKColor>)> GetRules() => new()
    {
        { (@"//.*", theme => theme.Comment) },
        { ("\".*?\"", theme => theme.String) },
        { (@"\b(if|else|return|class|void|public)\b", theme => theme.Keyword) },
        { (@"\b[A-Z]\w*\b", theme => theme.Type) },
        { (@"\b\w+(?=\s*\()", theme => theme.Method) }
    };
}