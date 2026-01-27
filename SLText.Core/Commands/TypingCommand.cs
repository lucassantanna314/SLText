using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class TypingCommand : ICommand
{
    private readonly TextBuffer _buffer;
    private readonly CursorManager _cursor;
    private TextMemento _beforeState; 
    public TypingCommand(TextBuffer buffer, CursorManager cursor)
    {
        _buffer = buffer;
        _cursor = cursor;
        _beforeState = _buffer.TakeSnapshot(_cursor.Line, _cursor.Column);
    }

    public void Execute() 
    {
        
    }

    public void Undo()
    {
        _buffer.RestoreSnapshot(_beforeState);
        _cursor.SetPosition(_beforeState.CursorLine, _beforeState.CursorColumn);    }
}