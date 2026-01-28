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

        string normalized = _textToPaste.Replace("\r\n", "\n").Replace("\r", "\n");
        string[] lines = normalized.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            _buffer.Insert(_cursor.Line, _cursor.Column, lines[i]);
            
            for (int j = 0; j < lines[i].Length; j++) _cursor.MoveRight();

            if (i < lines.Length - 1)
            {
                _buffer.BreakLine(_cursor.Line, _cursor.Column);
                _cursor.SetPosition(_cursor.Line + 1, 0); 
            }
        }
        
        _afterState = _buffer.TakeSnapshot(_cursor.Line, _cursor.Column);
    }

    public void Undo()
    {
        _buffer.RestoreSnapshot(_beforeState);
        _cursor.SetPosition(_beforeState.CursorLine, _beforeState.CursorColumn);
    }
}