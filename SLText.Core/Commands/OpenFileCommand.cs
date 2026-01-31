using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class OpenFileCommand : ICommand
{
    private readonly IDialogService _dialogService;
    private readonly TextBuffer _buffer;
    private readonly Action<string> _onSuccess;
    private readonly Func<string> _getLastDirectory;
    private readonly SaveFileCommand _saveCommand;
    private readonly Func<bool> _isDirtyCheck;
    private readonly UndoManager _undoManager;

    public OpenFileCommand(
        IDialogService dialogService, 
        TextBuffer buffer, 
        Action<string> onSuccess, 
        Func<string> getLastDirectory,
        SaveFileCommand saveCommand,
        Func<bool> isDirtyCheck,
        UndoManager undoManager)
    {
        _dialogService = dialogService;
        _buffer = buffer;
        _onSuccess = onSuccess;
        _getLastDirectory = getLastDirectory;
        _saveCommand = saveCommand;
        _isDirtyCheck = isDirtyCheck;
        _undoManager = undoManager;
    }

    public void Execute()
    {
        if (_isDirtyCheck())
        {
            _dialogService.SetModalCallbacks(
                onYes: () => 
                {
                    _saveCommand.Execute();
                    if (!_isDirtyCheck()) PromptOpenFile(); 
                },
                onNo: () => 
                {
                    PromptOpenFile(); 
                },
                onCancel: () => { }
            );

            _dialogService.AskToSave("Arquivo Atual");
        }
        else
        {
            PromptOpenFile();
        }
    }
    
    private void PromptOpenFile()
    {
        string filter = "txt,cs,html,css,js,razor,xml,rhex,json";
        string? path = _dialogService.OpenFile(filter, _getLastDirectory());

        if (path != null)
        {
            string content = File.ReadAllText(path).Replace("\t", "    ");
            _buffer.LoadText(content);
            _undoManager.Clear();
            _onSuccess?.Invoke(path); 
        }
    }

    public void Undo() { }
}