using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class OpenFileCommand : ICommand
{
    private readonly IDialogService _dialogService;
    private readonly Action<string?, bool> _onSuccess;
    private readonly Func<string> _getLastDirectory;

    public OpenFileCommand(
        IDialogService dialogService, 
        TextBuffer buffer, 
        Action<string?, bool> onSuccess, 
        Func<string> getLastDirectory,
        SaveFileCommand saveCommand,
        Func<bool> isDirtyCheck,
        UndoManager undoManager)
    {
        _dialogService = dialogService;
        _onSuccess = onSuccess;
        _getLastDirectory = getLastDirectory;
    }

    public void Execute()
    {
        string filter = "txt,cs,html,css,js,razor,xml,rhex,json";
        string? path = _dialogService.OpenFile(filter, _getLastDirectory());

        if (path != null)
        {
            _onSuccess?.Invoke(path, true); 
        }
    }

    public void Undo() { }
}