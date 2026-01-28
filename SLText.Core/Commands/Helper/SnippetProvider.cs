namespace SLText.Core.Commands.Helper;

public class SnippetProvider
{
    private static readonly Dictionary<string, string> _snippets = new()
    {
        // --- C# ---
        { "cw", "Console.WriteLine(|);" },
        
        // --- HTML ---
        { "div", "<div>|</div>" },
        { "a", "<a href=\"#\">|</a>" },
        { "img", "<img src=\"|\" alt=\"\" />" },
        { "ul", "<ul>\n    <li>|</li>\n</ul>" },
        { "html5", "<!DOCTYPE html>\n<html>\n<head>\n    <title>|</title>\n</head>\n<body>\n    \n</body>\n</html>" },
        { "script", "<script src=\"|\"></script>" },
        { "link", "<link rel=\"stylesheet\" href=\"|\">" },
        
        // --- CSS ---
        { "flex", "display: flex;\njustify-content: |;\nalign-items: center;" },
        { "media", "@media (max-width: |px) {\n    \n}" },

    }; 
    
    public static string? GetSnippet(string word) => 
        _snippets.TryGetValue(word, out var s) ? s : null;
}