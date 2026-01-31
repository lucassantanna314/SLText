using SLText.Core.Interfaces;

namespace SLText.Core.Engine.Strategies;

public class HtmlIndentationStrategy : IIndentationStrategy
{
    public string GetIndentation(string currentLine, int column)
    {
        string indent = "";
        foreach (char c in currentLine)
        {
            if (c == ' ' || c == '\t') indent += c;
            else break;
        }
        return indent;
    }

    public bool ShouldExpandBraces(string currentLine, int column)
    {
        if (column <= 0 || column >= currentLine.Length) return false;

        bool openTag = currentLine[column - 1] == '>';
        bool closeTag = currentLine.Length > column + 1 && 
                        currentLine[column] == '<' && 
                        currentLine[column + 1] == '/';

        return openTag && closeTag;
    }
}