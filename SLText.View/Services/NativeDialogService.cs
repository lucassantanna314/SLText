using NativeFileDialogSharp;
using SLText.Core.Interfaces;

namespace SLText.View.Services;

public class NativeDialogService : IDialogService
{
    public string? OpenFile(string filter, string defaultDirectory)
    {
        var result = Dialog.FileOpen(filter, defaultDirectory);
        return result.IsOk ? result.Path : null;
    }

    public string? SaveFile(string filter, string defaultDirectory)
    {
        var result = Dialog.FileSave(filter, defaultDirectory);
        return result.IsOk ? result.Path : null;
    }
}