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

        // Caso especial: início da linha (une com a linha de cima)
        if (col <= 0)
        {
            if (line > 0)
            {
                _buffer.Backspace(line, col); 
                _cursor.SetPosition(line - 1, _buffer.GetLineLength(line - 1)); 
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
        _buffer.RemoveRange(line, i, line, col);
        
        _cursor.SetPosition(line, i);
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