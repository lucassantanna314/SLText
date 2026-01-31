using SLText.Core.Interfaces;

namespace SLText.Core.Engine.Strategies;

public static class IndentationProvider
{
    private static readonly Dictionary<string, IIndentationStrategy> _strategies = new()
    {
        { ".cs", new CurlyBraceStrategy() },
        { ".js", new CurlyBraceStrategy() },
        { ".css", new CurlyBraceStrategy() },
        { ".html", new HtmlIndentationStrategy() },
        { ".xml", new HtmlIndentationStrategy() },
        { "default", new CurlyBraceStrategy() }
    };

    public static IIndentationStrategy GetStrategy(string? extension)
    {
        if (string.IsNullOrEmpty(extension) || !_strategies.ContainsKey(extension))
            return _strategies["default"];
            
        return _strategies[extension];
    }
}