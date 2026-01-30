using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class MoveLineCommand : ICommand
{
    private readonly TextBuffer _buffer;
    private readonly CursorManager _cursor;
    private readonly int _direction; 
    private bool _wasSelectionActive;

    public MoveLineCommand(TextBuffer buffer, CursorManager cursor, int direction)
    {
        _buffer = buffer;
        _cursor = cursor;
        _direction = direction;
    }

    public void Execute()
    {
        var range = _cursor.GetSelectionRange();

        if (range == null)
        {
            MoveSingleLine(_cursor.Line, _direction);
        }
        else
        {
            var (startL, startC, endL, endC) = range.Value;

            if (_direction == -1 && startL <= 0) return;
            if (_direction == 1 && endL >= _buffer.LineCount - 1) return;

            if (_direction == -1) 
            {
                var lineAboveText = _buffer.GetLines().ElementAt(startL - 1);
                _buffer.RemoveLine(startL - 1);
                _buffer.InsertLine(endL, lineAboveText);
            }
            else 
            {
                var lineBelowText = _buffer.GetLines().ElementAt(endL + 1);
                _buffer.RemoveLine(endL + 1);
                _buffer.InsertLine(startL, lineBelowText);
            }

            bool anchorIsAtTop = _cursor.SelectionAnchorLine < _cursor.Line;
            
            if (anchorIsAtTop)
                _cursor.UpdateSelectionRange(startL + _direction, endL + _direction);
            else
                _cursor.UpdateSelectionRange(endL + _direction, startL + _direction);
            
            _cursor.SetPosition(_cursor.Line, _cursor.Column);
        }
    }
    
    private void MoveSingleLine(int line, int dir)
    {
        int target = line + dir;
        if (target < 0 || target >= _buffer.LineCount) return;

        var content = _buffer.GetLines().ElementAt(line);
        _buffer.RemoveLine(line);
        _buffer.InsertLine(target, content);
        _cursor.SetPosition(target, _cursor.Column);
    }

    public void Undo()
    {
        new MoveLineCommand(_buffer, _cursor, -_direction).Execute();
    }
}