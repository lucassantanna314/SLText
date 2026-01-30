using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class SaveFileCommand : ICommand
{
    private readonly IDialogService _dialogService;
    private readonly TextBuffer _buffer;
    private readonly Action<string> _onSuccess;
    private string? _currentPath;
    private readonly Func<string> _getLastDirectory;

    public SaveFileCommand(IDialogService dialogs, TextBuffer buffer, Action<string> onSuccess, Func<string> getLastDir)
    {
        _dialogService = dialogs;
        _buffer = buffer;
        _onSuccess = onSuccess;
        _getLastDirectory = getLastDir;
    }
    
    public void SetPath(string path)
    {
        _currentPath = path;
    }

    public void Execute()
    {
        string? path = _currentPath;

        if (string.IsNullOrEmpty(path))
        {
            string filter = "txt,cs,html,htm,css,js,razor,rhex,json";
            path = _dialogService.SaveFile(filter, _getLastDirectory());
        }

        if (path != null)
        {
            try
            {
                File.WriteAllText(path, _buffer.GetAllText());
                _currentPath = path; 
                _onSuccess?.Invoke(path); 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao salvar: {ex.Message}");
            }
        }
    }

    public void Undo() {  }
}