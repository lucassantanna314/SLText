using SLText.Core.Engine;
using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class SaveFileCommand : ICommand
{
    private readonly IDialogService _dialogService;
    private readonly TextBuffer _buffer;
    private readonly Action<string> _onSuccess;
    private string? _currentPath;

    public SaveFileCommand(IDialogService dialogService, TextBuffer buffer, Action<string> onSuccess, string? currentPath = null)
    {
        _dialogService = dialogService;
        _buffer = buffer;
        _onSuccess = onSuccess;
        _currentPath = currentPath;
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
            string filter = "txt,cs,html,htm,css,js,razor,cshtml,xml,csproj,gcode,nc,cnc,tap";
            path = _dialogService.SaveFile(filter, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
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