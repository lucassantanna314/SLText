using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class DeleteSelectionCommand(TextBuffer buffer, CursorManager cursor) : ICommand
{
    private readonly TextBuffer _buffer = buffer;
    private readonly CursorManager _cursor = cursor;
    private TextMemento? _snapshot;

    public void Execute()
    {
        _snapshot = _buffer.TakeSnapshot(_cursor.Line, _cursor.Column);
        var range = _cursor.GetSelectionRange();
    
        if (range != null)
        {
            var r = range.Value;
            _buffer.RemoveRange(r.startLine, r.startCol, r.endLine, r.endCol);
        
            _cursor.SetPosition(r.startLine, r.startCol);
        
            _cursor.ClearSelection();
        }
    }

    public void Undo()
    {
        _buffer.RestoreSnapshot(_snapshot!);
        _cursor.SetPosition(_snapshot!.CursorLine, _snapshot.CursorColumn);
    }
}