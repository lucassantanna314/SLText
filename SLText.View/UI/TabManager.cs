using Microsoft.CodeAnalysis;
using SLText.Core.Engine;
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

        if (_editor != null)
        {
            // SetCurrentData must run before SetScroll/UpdateSyntax: it re-points the renderer at the
            // tab's buffer, and the old code also called UpdateSyntax twice per switch, re-running a
            // full re-highlight of the file for no reason.
            _editor.SetCurrentData(active.Buffer, active.Cursor);

            if (resetCursor)
            {
                _editor.SetScroll(0, 0);
                active.SavedScrollX = 0;
                active.SavedScrollY = 0;
                active.Cursor.SetPosition(0, 0);
            }
            else
            {
                _editor.SetScroll(active.SavedScrollX, active.SavedScrollY);
            }

            string language = _editor.UpdateSyntax(active.FilePath);

            if (!resetCursor) _editor.RequestScrollToCursor();

            if (_statusBar != null)
            {
                _statusBar.UpdateActiveBuffer(active.Buffer, active.Cursor);
                _statusBar.LanguageName = language;
                _statusBar.FileInfo = active.Title;
            }
        }

        _inputHandler?.UpdateActiveData(active.Cursor, active.Buffer);
        _mouseHandler?.UpdateActiveCursor(active.Cursor);
        _inputHandler?.UpdateCurrentPath(_currentFilePath);
        
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

        if (!active.IsDirty)
        {
            FinishClosingTab();
            return;
        }

        _modal.Show(
            title: "Salvar alterações?",
            message: $"O arquivo '{active.Title}' possui alterações não salvas. Deseja salvar antes de fechar?",
            onYes: () =>
            {
                _inputHandler.HandleShortcut(true, false, "S");
                FinishClosingTab();
            },
            onNo: () => FinishClosingTab(),
            onCancel: () => { }
        );
    }
    
    private void FinishClosingTab()
    {
        int index = _tabManager.ActiveTabIndex;
        if (index == -1) return;

        _tabManager.CloseTab(index);

        // Closing the last tab used to call SetCurrentFile(null), which returned immediately and left
        // the closed file's text on screen. Always keep at least one (empty) tab so the editor, the
        // status bar and the buffer/cursor references stay consistent.
        if (_tabManager.Tabs.Count == 0)
        {
            var buffer = new TextBuffer();
            _tabManager.AddTab(buffer, new CursorManager(buffer), null);
        }

        // Clear before syncing: SyncActiveTab re-requests diagnostics for the newly active tab, and
        // clearing afterwards used to wipe the results it had just produced.
        var cleared = new List<LspService.MappedDiagnostic>();
        _editor.SetDiagnostics(cleared);
        _terminal.ShowDiagnostics(cleared, _tabManager.ActiveTab?.Title ?? "New File");

        SyncActiveTab(false);
        SyncSettings();
    }
}