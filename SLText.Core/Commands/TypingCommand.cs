using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class TypingCommand : ICommand
{
    private readonly TextBuffer _buffer;
    private readonly CursorManager _cursor;
    private TextMemento _beforeState; 
    private TextMemento? _afterState;
    public TypingCommand(TextBuffer buffer, CursorManager cursor)
    {
        _buffer = buffer;
        _cursor = cursor;
        _beforeState = _buffer.TakeSnapshot(_cursor.Line, _cursor.Column);
    }
    
    public void FinalizeCommand()
    {
        _afterState = _buffer.TakeSnapshot(_cursor.Line, _cursor.Column);
    }
    public void Execute() 
    {
        if (_afterState != null)
        {
            _buffer.RestoreSnapshot(_afterState);
            _cursor.SetPosition(_afterState.CursorLine, _afterState.CursorColumn);
        } 
    }

    public void Undo()
    {
        _buffer.RestoreSnapshot(_beforeState);
        _cursor.SetPosition(_beforeState.CursorLine, _beforeState.CursorColumn);    }
}