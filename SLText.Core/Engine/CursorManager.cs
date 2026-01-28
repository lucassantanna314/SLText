namespace SLText.Core.Engine;

public class CursorManager
{
    private readonly TextBuffer _buffer;
    
    public int? SelectionAnchorLine { get; private set; }
    public int? SelectionAnchorColumn { get; private set; }
    
    public bool HasSelection => SelectionAnchorLine.HasValue;

    public CursorManager(TextBuffer buffer)
    {
        _buffer = buffer;
    }

    public int Line { get; private set; }
    public int Column { get; private set; }
    private int _desiredColumn = 0;
    
    //selection
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
    
    public (int startLine, int startCol, int endLine, int endCol)? GetSelectionRange()
    {
        if (!HasSelection) return null;

        var start = (l: SelectionAnchorLine.Value, c: SelectionAnchorColumn.Value);
        var end = (l: Line, c: Column);

        if (start.l < end.l || (start.l == end.l && start.c < end.c))
            return (start.l, start.c, end.l, end.c);
        
        return (end.l, end.c, start.l, start.c);
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

    public void Insert(char c)
    {
        _buffer.Insert(c, Line, Column);
        Column++;
        _desiredColumn = Column;
    }

    public void Enter()
    {
        var lines = _buffer.GetLines().ToList();
        string currentLineText = lines[Line];
        int safeColumn = Math.Clamp(Column, 0, currentLineText.Length);

        string baseIndentation = "";
        foreach (char c in currentLineText)
        {
            if (c == ' ' || c == '\t') baseIndentation += c;
            else break;
        }

        bool isBetweenBraces = safeColumn > 0 && safeColumn < currentLineText.Length &&
                               currentLineText[safeColumn - 1] == '{' && 
                               currentLineText[safeColumn] == '}';

        if (isBetweenBraces)
        {
            _buffer.Delete(Line, safeColumn); 

            _buffer.BreakLine(Line, safeColumn);
            Line++;

            string midIndentation = baseIndentation + "    ";
            _buffer.Insert(Line, 0, midIndentation);

            _buffer.BreakLine(Line, midIndentation.Length);
            _buffer.Insert(Line + 1, 0, baseIndentation + "}");

            Column = midIndentation.Length;
        }
        else
        {
            _buffer.BreakLine(Line, safeColumn);
            Line++;
        
            if (!string.IsNullOrEmpty(baseIndentation))
            {
                _buffer.Insert(Line, 0, baseIndentation);
                Column = baseIndentation.Length;
            }
            else
            {
                Column = 0;
            }
        }

        _desiredColumn = Column;
    }
    
    public void SelectAll()
    {
        SelectionAnchorLine = 0;
        SelectionAnchorColumn = 0;

        Line = _buffer.LineCount - 1;
        Column = _buffer.GetLineLength(Line);
    
        _desiredColumn = Column;
    }
    
    
    public void Backspace()
    {
        if (Column > 0)
        {
            _buffer.Backspace(Line, Column);
            Column--;
        }
        else if (Line > 0)
        {
            int targetColumn = _buffer.GetLineLength(Line - 1);
            _buffer.Backspace(Line,Column);
            Line--;
            Column = targetColumn;
        }
        
        _desiredColumn = Column;
    }

    public void Delete()
    {
        _buffer.Delete(Line, Column);
    }
    
    public void SetPosition(int line, int column)
    {
        Line = Math.Clamp(line, 0, _buffer.LineCount - 1);
        Column = Math.Clamp(column, 0, _buffer.GetLineLength(Line));
        _desiredColumn = Column;
    }
    
    private void UpdateColumnToDesired()
    {
        Column = Math.Min(_desiredColumn, _buffer.GetLineLength(Line));
    }
}