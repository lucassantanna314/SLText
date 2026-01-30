using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class OpenFileCommand : ICommand
{
    private readonly IDialogService _dialogService;
    private readonly TextBuffer _buffer;
    private readonly Action<string> _onSuccess;
    private readonly Func<string> _getLastDirectory;

    public OpenFileCommand(IDialogService dialogService, TextBuffer buffer, Action<string> onSuccess, Func<string> getLastDirectory)
    {
        _dialogService = dialogService;
        _buffer = buffer;
        _onSuccess = onSuccess;
        _getLastDirectory = getLastDirectory;
    }

    public void Execute()
    {
        string filter = "txt,cs,html,css,js,razor,xml,rhex,json";
        
        string? path = _dialogService.OpenFile(filter, _getLastDirectory());

        if (path != null)
        {
            string content = File.ReadAllText(path).Replace("\t", "    ");
            _buffer.LoadText(content);
            _onSuccess?.Invoke(path); 
        }
    }

    public void Undo() { }
}