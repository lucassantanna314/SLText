namespace SLText.Core.Interfaces;

public interface IIndentationStrategy
{
    string GetIndentation(string currentLine, int column);
    
    bool ShouldExpandBraces(string currentLine, int column);
}