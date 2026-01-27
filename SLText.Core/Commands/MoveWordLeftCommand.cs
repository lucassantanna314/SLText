using SLText.Core.Interfaces;
using SLText.Core.Engine;

namespace SLText.Core.Commands;

public class MoveWordLeftCommand : ICommand
{
    private readonly CursorManager _cursor;
    private readonly TextBuffer _buffer;

    public MoveWordLeftCommand(CursorManager cursor, TextBuffer buffer)
    {
        _cursor = cursor;
        _buffer = buffer;
    }

    public void Execute()
    {
        int line = _cursor.Line;
        int col = _cursor.Column;

        if (col <= 0)
        {
            if (line > 0)
            {
                int prevLine = line - 1;
                _cursor.SetPosition(prevLine, _buffer.GetLineLength(prevLine));
            }
            return;
        }

        string text = _buffer.GetLines().ElementAt(line);
        int i = col - 1;

        while (i > 0 && char.IsWhiteSpace(text[i])) i--;

        while (i > 0 && !char.IsWhiteSpace(text[i - 1])) i--;

        _cursor.SetPosition(line, i);
    }

    public void Undo()
    {
        
    }
}