using Microsoft.CodeAnalysis;
using SLText.Core.Engine.LSP;

namespace SLText.View.UI;

public partial class WindowManager
{
    private void SyncActiveTab(bool resetCursor)
    {
        if (_tabManager.ActiveTab == null) return;
    
        var active = _tabManager.ActiveTab!;
        _currentFilePath = active.FilePath;
        _buffer = active.Buffer;
        _cursor = active.Cursor;

        _explorer?.SetSelectedFile(active.FilePath);
        _editor?.SetCurrentData(active.Buffer, active.Cursor);
    
        if (_editor != null)
        {
            if (resetCursor) 
            {
                _editor.SetScroll(0, 0);
                active.SavedScrollX = 0;
                active.SavedScrollY = 0;
            }
            else 
            {
                _editor.SetScroll(active.SavedScrollX, active.SavedScrollY);
            }        
            _editor.UpdateSyntax(active.FilePath);
            if (!resetCursor) _editor.RequestScrollToCursor();
        }
    
        _inputHandler?.UpdateActiveData(active.Cursor, active.Buffer);
        _mouseHandler?.UpdateActiveCursor(active.Cursor);
    
        if (_statusBar != null)
        {
            _statusBar.UpdateActiveBuffer(active.Buffer, active.Cursor);
            _statusBar.LanguageName = _editor?.UpdateSyntax(_currentFilePath) ?? "Plain Text";
            _statusBar.FileInfo = active.Title;
        }

        _inputHandler?.UpdateCurrentPath(_currentFilePath);
        if (resetCursor) active.Cursor.SetPosition(0, 0);
        
        if (_window.Size.X > 0)
        {
            _tabComponent?.EnsureActiveTabVisible();
        }
        
        UpdateTitle();
        
        if (!_isLoadingSession && !string.IsNullOrEmpty(_currentFilePath) && File.Exists(_currentFilePath))
        {
            RequestDiagnostics(instant: true);
        }
    }
    
    public void CloseActiveTab()
    {
        var active = _tabManager.ActiveTab;
        if (active == null) return;

        if (active.IsDirty)
        {
            _modal.Show(
                title: "Salvar alterações?",
                message: $"O arquivo '{active.Title}' possui alterações não salvas. Deseja salvar antes de fechar?",
                onYes: () =>
                {
                    _inputHandler.HandleShortcut(true, false, "S");
                    FinishClosingTab();
                    _editor.SetDiagnostics(new List<LspService.MappedDiagnostic>());
                    _terminal.ShowDiagnostics((new List<LspService.MappedDiagnostic>()), active.Title);
                    RequestDiagnostics();
                },
                onNo: () =>
                {
                    _editor.SetDiagnostics(new List<LspService.MappedDiagnostic>());
                    _terminal.ShowDiagnostics(new List<LspService.MappedDiagnostic>(), active.Title);
                    RequestDiagnostics();
                    FinishClosingTab();
                    
                },
                onCancel: () =>
                {
                }
            );
        }
        else
        {
            FinishClosingTab();
            _editor.SetDiagnostics(new List<LspService.MappedDiagnostic>());
            _terminal.ShowDiagnostics(new List<LspService.MappedDiagnostic>(), active.Title);
            RequestDiagnostics();
        }
    }
    
    private void FinishClosingTab()
    {
        int index = _tabManager.ActiveTabIndex;
        if (index == -1) return;

        _tabManager.CloseTab(index);

        if (_tabManager.Tabs.Count == 0)
        {
            SetCurrentFile(null);
        }
        else
        {
            SyncActiveTab(false);
        }
        
        SyncSettings();
    }
}