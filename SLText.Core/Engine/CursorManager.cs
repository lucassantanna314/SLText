namespace SLText.Core.Engine;

public class CursorManager
{
    private readonly TextBuffer _buffer;

    public CursorManager(TextBuffer buffer)
    {
        _buffer = buffer;
    }

    public int Line { get; private set; }
    public int Column { get; private set; }
    private int _desiredColumn = 0;

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
        _buffer.BreakLine(Line, Column);
        Line++;
        Column = 0;
        _desiredColumn = 0;
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