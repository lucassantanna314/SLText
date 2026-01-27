namespace SLText.Core.Engine;

public record TextMemento(List<List<char>> Lines, int CursorLine, int CursorColumn);