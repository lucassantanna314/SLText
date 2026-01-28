using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class DeleteWordLeftCommand : ICommand
{
    private readonly CursorManager _cursor;
    private readonly TextBuffer _buffer;
    private TextMemento? _snapshot;
    
    public DeleteWordLeftCommand(CursorManager cursor, TextBuffer buffer)
    {
        _cursor = cursor;
        _buffer = buffer;
    }
    
    public void Execute()
    {
        _snapshot = _buffer.TakeSnapshot(_cursor.Line, _cursor.Column);
        
        int line = _cursor.Line;
        int col = _cursor.Column;

        if (col <= 0)
        {
            if (line > 0)
            {
                _cursor.Backspace();
            }
            return;
        }

        string text = _buffer.GetLines().ElementAt(line);
        int i = col;

        while (i > 0 && char.IsWhiteSpace(text[i - 1])) i--;

        if (i > 0)
        {
            bool isLetterOrDigit = char.IsLetterOrDigit(text[i - 1]);

            while (i > 0 && !char.IsWhiteSpace(text[i - 1]) && char.IsLetterOrDigit(text[i - 1]) == isLetterOrDigit)
            {
                i--;
            }
        }

        int countToDelete = col - i;

        for (int k = 0; k < countToDelete; k++)
        {
            _buffer.Backspace(line, _cursor.Column);
            _cursor.MoveLeft();
        }
    }
    
    public void Undo()
    {
        if (_snapshot != null)
        {
            _buffer.RestoreSnapshot(_snapshot);
            _cursor.SetPosition(_snapshot.CursorLine, _snapshot.CursorColumn);
        }
    }
}