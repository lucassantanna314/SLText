using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class BackspaceCommand : ICommand
{
    private readonly TextBuffer _buffer;
    private readonly CursorManager _cursor;
    private TextMemento? _snapshot;

    public BackspaceCommand(TextBuffer buffer, CursorManager cursor)
    {
        _buffer = buffer;
        _cursor = cursor;
    }

    public void Execute()
    {
        _snapshot = _buffer.TakeSnapshot(_cursor.Line, _cursor.Column);

        int line = _cursor.Line;
        int col = _cursor.Column;

        if (col > 0)
        {
            _buffer.Backspace(line, col);
            _cursor.SetPosition(line, col - 1);
        }
        else if (line > 0)
        {
            int targetColumn = _buffer.GetLineLength(line - 1);
            _buffer.Backspace(line, col);
            _cursor.SetPosition(line - 1, targetColumn);
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