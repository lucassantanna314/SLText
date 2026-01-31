using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class DuplicateLineCommand : ICommand
{
    private readonly TextBuffer _buffer;
    private readonly CursorManager _cursor;
    private int _originalLine;
    private int _originalCol;

    public DuplicateLineCommand(TextBuffer buffer, CursorManager cursor)
    {
        _buffer = buffer;
        _cursor = cursor;
    }

    public void Execute()
    {
        _originalLine = _cursor.Line;
        _originalCol = _cursor.Column;

        var currentLineText = _buffer.GetLines().ElementAtOrDefault(_originalLine);
    
        if (currentLineText != null)
        {
            _buffer.InsertLine(_originalLine + 1, currentLineText);
        
            _cursor.SetPosition(_originalLine + 1, _originalCol);
        }
    }

    public void Undo()
    {
        _buffer.RemoveLine(_originalLine + 1);
        _cursor.SetPosition(_originalLine, _originalCol);
    }
}