using SLText.Core.Interfaces;

namespace SLText.Core.Engine.Strategies;

public class CurlyBraceStrategy : IIndentationStrategy
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
        return column > 0 && column < currentLine.Length &&
               currentLine[column - 1] == '{' && currentLine[column] == '}';
    }
}