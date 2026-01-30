using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class NewFileCommand : ICommand
{
    private readonly TextBuffer _buffer;
    private readonly CursorManager _cursor;
    private readonly IDialogService _dialogService;
    private readonly SaveFileCommand _saveCommand;
    private readonly Func<bool> _isDirtyCheck;
    private readonly Action<string?, bool> _onSuccess;
    private readonly UndoManager _undoManager;

    public NewFileCommand(
        TextBuffer buffer, 
        CursorManager cursor, 
        IDialogService dialogService,
        SaveFileCommand saveCommand,
        Func<bool> isDirtyCheck,
        Action<string?, bool> onSuccess,
        UndoManager undoManager)
    {
        _buffer = buffer;
        _cursor = cursor;
        _dialogService = dialogService;
        _saveCommand = saveCommand;
        _isDirtyCheck = isDirtyCheck;
        _onSuccess = onSuccess;
        _undoManager = undoManager;
    }

    public void Execute()
    {
        if (_isDirtyCheck())
        {
            _dialogService.SetModalCallbacks(
                onYes: () => {
                    _saveCommand.Execute();
                    if (!_isDirtyCheck()) FinalizeNewFile();
                },
                onNo: () => FinalizeNewFile(),
                onCancel: () => { }
            );

            _dialogService.AskToSave("Arquivo Atual");
        }
        else
        {
            FinalizeNewFile();
        }
    }
    
    private void FinalizeNewFile()
    {
        _buffer.LoadText("");
        _cursor.SetPosition(0, 0);
        _undoManager.Clear(); 
        _onSuccess?.Invoke(null, true); 
    }

    public void Undo() {  }
}