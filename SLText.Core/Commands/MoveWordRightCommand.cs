using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class MoveWordRightCommand : ICommand
{
    private readonly CursorManager _cursor;
    private readonly TextBuffer _buffer;

    public MoveWordRightCommand(CursorManager cursor, TextBuffer buffer)
    {
        _cursor = cursor;
        _buffer = buffer;
    }
    
    public void Execute()
    {
        int line = _cursor.Line;
        int col = _cursor.Column;
        int lineLength = _buffer.GetLineLength(line);
        
        if (col >= lineLength)
        {
            if (line < _buffer.LineCount - 1)
                _cursor.SetPosition(line + 1, 0);
            return;
        }
        
        string text = _buffer.GetLines().ElementAt(line);
        int i = col;
        
        while (i < lineLength && char.IsWhiteSpace(text[i])) i++;
        while (i < lineLength && !char.IsWhiteSpace(text[i])) i++;
        
        _cursor.SetPosition(line, i);
    }

    public void Undo()
    {
       
    }
}