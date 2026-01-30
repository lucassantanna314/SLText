using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class OpenFileCommand : ICommand
{
    private readonly IDialogService _dialogService;
    private readonly TextBuffer _buffer;
    private readonly Action<string> _onSuccess;

    public OpenFileCommand(IDialogService dialogService, TextBuffer buffer, Action<string> onSuccess)
    {
        _dialogService = dialogService;
        _buffer = buffer;
        _onSuccess = onSuccess;
    }

    public void Execute()
    {
        string filter = "txt,cs,html,css,js,razor,xml";
        string? path = _dialogService.OpenFile(filter, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        if (path != null)
        {
            string content = File.ReadAllText(path).Replace("\t", "    ");
            _buffer.LoadText(content);
            _onSuccess?.Invoke(path); 
        }
    }

    public void Undo() { }
}