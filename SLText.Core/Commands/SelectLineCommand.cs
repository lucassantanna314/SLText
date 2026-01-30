using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class SelectLineCommand : ICommand
{
    private readonly CursorManager _cursor;
    private readonly TextBuffer _buffer;
    
    private int? _oldAnchorLine;
    private int? _oldAnchorCol;
    private int _oldCursorLine;
    private int _oldCursorCol;

    public SelectLineCommand(CursorManager cursor, TextBuffer buffer)
    {
        _cursor = cursor;
        _buffer = buffer;
    }

    public void Execute()
    {
        _oldAnchorLine = _cursor.SelectionAnchorLine;
        _oldAnchorCol = _cursor.SelectionAnchorColumn;
        _oldCursorLine = _cursor.Line;
        _oldCursorCol = _cursor.Column;

        _cursor.ClearSelection();
        
        _cursor.SetPosition(_cursor.Line, 0);
        
        _cursor.StartSelection();
        
        int lineLength = _buffer.GetLineLength(_cursor.Line);
        _cursor.SetPosition(_cursor.Line, lineLength);
    }

    public void Undo()
    {
        _cursor.UpdateSelectionRange(_oldAnchorLine ?? _oldCursorLine, _oldCursorLine);
        _cursor.SetPosition(_oldCursorLine, _oldCursorCol);
        
        if (!_oldAnchorLine.HasValue) 
            _cursor.ClearSelection();
    }
}