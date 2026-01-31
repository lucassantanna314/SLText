using NativeFileDialogSharp;
using SLText.Core.Interfaces;
using SLText.View.Components;

namespace SLText.View.Services;

public class NativeDialogService : IDialogService
{
    public ModalComponent? Modal { get; set; }
    
    public string? OpenFile(string filter, string defaultDirectory)
    {
        var result = Dialog.FileOpen(filter, defaultDirectory);
        Modal?.TriggerRecentlyClosed();
        return result.IsOk ? result.Path : null;
    }

    public string? SaveFile(string filter, string defaultDirectory)
    {
        var result = Dialog.FileSave(filter, defaultDirectory);
        Modal?.TriggerRecentlyClosed();
        return result.IsOk ? result.Path : null;
    }
    
    public void SetModalCallbacks(Action? onYes, Action? onNo, Action? onCancel)
    {
        if (Modal == null) return;
        Modal.OnYes = onYes;
        Modal.OnNo = onNo;
        Modal.OnCancel = onCancel;
    }

    public bool? AskToSave(string fileName)
    {
        if (Modal == null) return false; 

        Modal.Show(
            "Salvar Alterações?",
            $"O arquivo '{fileName}' foi modificado. Deseja salvar?",
            onYes: Modal.OnYes, 
            onNo: Modal.OnNo,
            onCancel: Modal.OnCancel
        );
        
        return null;
    }
}