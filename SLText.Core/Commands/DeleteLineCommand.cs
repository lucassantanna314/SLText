using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class DeleteLineCommand : ICommand
{
    private readonly TextBuffer _buffer;
    private readonly CursorManager _cursor;
    private string _deletedText = "";
    private int _deletedLineIndex;

    public DeleteLineCommand(TextBuffer buffer, CursorManager cursor)
    {
        _buffer = buffer;
        _cursor = cursor;
    }

    public void Execute()
    {
        _deletedLineIndex = _cursor.Line;
        _deletedText = _buffer.GetLines().ElementAt(_deletedLineIndex);

        _buffer.RemoveLine(_deletedLineIndex);

        if (_cursor.Line >= _buffer.LineCount)
        {
            _cursor.SetPosition(Math.Max(0, _buffer.LineCount - 1), 0);
        }
        else
        {
            _cursor.SetPosition(_cursor.Line, 0);
        }
    }

    public void Undo()
    {
        _buffer.InsertLine(_deletedLineIndex, _deletedText);
        _cursor.SetPosition(_deletedLineIndex, 0);
    }
}