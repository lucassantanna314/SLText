using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class NewFileCommand : ICommand
{
    private readonly Action<string?, bool> _onSuccess;

    public NewFileCommand(
        TextBuffer buffer, 
        CursorManager cursor, 
        IDialogService dialogService,
        SaveFileCommand saveCommand,
        Func<bool> isDirtyCheck,
        Action<string?, bool> onSuccess,
        UndoManager undoManager)
    {
        _onSuccess = onSuccess;
    }

    public void Execute()
    {
        _onSuccess?.Invoke(null, true);
    }

    public void Undo() { }
}