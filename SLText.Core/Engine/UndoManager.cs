namespace SLText.Core.Engine;

using SLText.Core.Interfaces;

public class UndoManager
{
    private readonly LinkedList<ICommand> _history = new();
    private readonly Stack<ICommand> _redoStack = new();
    private const int MaxHistory = 200;
    
    public int HistoryCount => _history.Count;
    public int RedoCount => _redoStack.Count;
    
    public void ExecuteCommand(ICommand command)
    {
        command.Execute();
        AddtoHistory(command);
        _redoStack.Clear();
    }
    
    private void AddtoHistory(ICommand command)
    {
        _history.AddLast(command);
        if (_history.Count > MaxHistory)
        {
            _history.RemoveFirst();
        }
    }

    public void Undo()
    {
        if (_history.Count == 0) return;

        var command = _history.Last.Value;
        _history.RemoveLast();
        
        command.Undo();
        _redoStack.Push(command);
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;

        var command = _redoStack.Pop();
        command.Execute();
        
        _history.AddLast(command);
        if (_history.Count > MaxHistory)
        {
            _history.RemoveFirst();
        }
    }
    
    public void AddExternalCommand(ICommand command)
    {
        _history.AddLast(command);
        if (_history.Count > MaxHistory) _history.RemoveFirst();
        _redoStack.Clear();
    }
}