namespace SLText.Core.Interfaces;

public interface IDialogService
{
    string? OpenFile(string filter, string defaultDirectory);
    string? SaveFile(string filter, string defaultDirectory);
}