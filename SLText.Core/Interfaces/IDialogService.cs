namespace SLText.Core.Interfaces;

public interface IDialogService
{
    string? OpenFile(string filter, string defaultDirectory);
    string? SaveFile(string filter, string defaultDirectory);
    bool? AskToSave(string fileName);
    void SetModalCallbacks(Action? onYes, Action? onNo, Action? onCancel);
}