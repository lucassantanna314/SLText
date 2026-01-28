using SLText.View.Styles.Languages;
using SkiaSharp;

namespace SLText.View.Styles;

public class SyntaxProvider
{
    private readonly List<LanguageDefinition> _languages;

    public SyntaxProvider()
    {
        _languages = new List<LanguageDefinition>
        {
            new CSharpDefinition(),
            new HtmlDefinition(),
            new CssDefinition(),
            new JsDefinition(),
            new RazorDefinition(),
            new XmlDefinition(),
            new GCodeDefinition()
        };
    }

    public List<(string pattern, SKColor color)> GetRulesForFile(string? filePath, EditorTheme theme)
    {
        if (string.IsNullOrEmpty(filePath)) return new();

        string ext = Path.GetExtension(filePath).ToLower();
        
        var lang = _languages.FirstOrDefault(l => l.Extensions.Contains(ext));
        
        if (lang == null) return new();

        return lang.GetRules()
            .Select(r => (r.Pattern, r.ColorSelector(theme)))
            .ToList();
    }
    
    public string GetLanguageName(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return "Plain Text";

        string ext = Path.GetExtension(filePath).ToLower();
        var lang = _languages.FirstOrDefault(l => l.Extensions.Contains(ext));

        return lang?.Name ?? "Plain Text";
    }
}