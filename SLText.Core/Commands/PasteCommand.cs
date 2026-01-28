using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class PasteCommand : ICommand
{
    private readonly TextBuffer _buffer;
    private readonly CursorManager _cursor;
    private readonly string _textToPaste;
    private TextMemento _beforeState;
    private TextMemento? _afterState;

    public PasteCommand(TextBuffer buffer, CursorManager cursor, string text)
    {
        _buffer = buffer;
        _cursor = cursor;
        _textToPaste = text;
        _beforeState = _buffer.TakeSnapshot(_cursor.Line, _cursor.Column);
    }

    public void Execute()
    {
        if (_afterState != null)
        {
            _buffer.RestoreSnapshot(_afterState);
            _cursor.SetPosition(_afterState.CursorLine, _afterState.CursorColumn);
            return;
        }

        foreach (char c in _textToPaste)
        {
            if (c == '\r') continue; 
            if (c == '\n') _cursor.Enter();
            else _cursor.Insert(c);
        }
        
        _afterState = _buffer.TakeSnapshot(_cursor.Line, _cursor.Column);
    }

    public void Undo()
    {
        _buffer.RestoreSnapshot(_beforeState);
        _cursor.SetPosition(_beforeState.CursorLine, _beforeState.CursorColumn);
    }
}