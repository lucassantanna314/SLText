namespace SLText.Core.Engine;

public class CursorManager
{
    private readonly TextBuffer _buffer;
    
    public int Line { get; private set; }
    public int Column { get; private set; }
    private int _desiredColumn = 0; 

    public int? SelectionAnchorLine { get; private set; }
    public int? SelectionAnchorColumn { get; private set; }
    public bool HasSelection => SelectionAnchorLine.HasValue;

    public CursorManager(TextBuffer buffer)
    {
        _buffer = buffer;
    }


    public void MoveUp()
    {
        Line = Math.Max(0, Line - 1);
        UpdateColumnToDesired();
    }

    public void MoveDown()
    {
        Line = Math.Min(_buffer.LineCount - 1, Line + 1);
        UpdateColumnToDesired();
    }
    
    public void MoveLeft()
    {
        if (Column > 0)
        {
            Column--;
        }
        else if (Line > 0)
        {
            Line--;
            Column = _buffer.GetLineLength(Line);
        }
        _desiredColumn = Column;
    }
    
    public void MoveRight()
    {
        if (Column < _buffer.GetLineLength(Line))
        {
            Column++;
        }
        else if (Line < _buffer.LineCount - 1)
        {
            Line++;
            Column = 0;
        }
        _desiredColumn = Column;
    }

    public void SetPosition(int line, int column)
    {
        Line = Math.Clamp(line, 0, _buffer.LineCount - 1);
        Column = Math.Clamp(column, 0, _buffer.GetLineLength(Line));
        _desiredColumn = Column;
    }
    
    public void SetSelection(int startLine, int startCol, int endLine, int endCol)
    {
        SelectionAnchorLine = startLine;
        SelectionAnchorColumn = startCol;

        Line = Math.Clamp(endLine, 0, _buffer.LineCount - 1);
        Column = Math.Clamp(endCol, 0, _buffer.GetLineLength(Line));
    
        _desiredColumn = Column;
    }

    private void UpdateColumnToDesired()
    {
        Column = Math.Min(_desiredColumn, _buffer.GetLineLength(Line));
    }

    public void Insert(char c)
    {
        _buffer.Insert(Line, Column, c);
        Column++;
        _desiredColumn = Column;
    }

    public void StartSelection()
    {
        if (!HasSelection)
        {
            SelectionAnchorLine = Line;
            SelectionAnchorColumn = Column;
        }
    }
    
    public void ClearSelection()
    {
        SelectionAnchorLine = null;
        SelectionAnchorColumn = null;
    }
    
    public void SelectAll()
    {
        SelectionAnchorLine = 0;
        SelectionAnchorColumn = 0;
        Line = _buffer.LineCount - 1;
        Column = _buffer.GetLineLength(Line);
        _desiredColumn = Column;
    }
    
    public void UpdateSelectionRange(int newAnchorLine, int newCursorLine)
    {
        SelectionAnchorLine = newAnchorLine;
        Line = newCursorLine;
        _desiredColumn = Column; 
    }
    
    public (int startLine, int startCol, int endLine, int endCol)? GetSelectionRange()
    {
        if (!HasSelection) return null;

        var start = (l: SelectionAnchorLine.Value, c: SelectionAnchorColumn.Value);
        var end = (l: Line, c: Column);

        if (start.l < end.l || (start.l == end.l && start.c < end.c))
            return (start.l, start.c, end.l, end.c);
        
        return (end.l, end.c, start.l, start.c);
    }
}