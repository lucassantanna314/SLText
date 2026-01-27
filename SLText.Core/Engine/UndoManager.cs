namespace SLText.Core.Engine;

using SLText.Core.Interfaces;

public class UndoManager
{
    private readonly Stack<ICommand> _history = new();
    private readonly Stack<ICommand> _redoStack = new();
    
    public int HistoryCount => _history.Count;
    public int RedoCount => _redoStack.Count;
    
    public void ExecuteCommand(ICommand command)
    {
        command.Execute();
        _history.Push(command);
        
        _redoStack.Clear();
    }

    public void Undo()
    {
        if (_history.Count > 0)
        {
            var command = _history.Pop();
            command.Undo();
            _redoStack.Push(command); 
        }
    }

    public void Redo()
    {
        if (_redoStack.Count > 0)
        {
            var command = _redoStack.Pop();
            command.Execute(); 
            _history.Push(command);
        }
    }
}