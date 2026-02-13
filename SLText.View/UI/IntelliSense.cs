using Microsoft.CodeAnalysis;
using SLText.Core.Engine.LSP;

namespace SLText.View.UI;

public partial class WindowManager
{
    private void RequestDiagnostics(bool instant = false)
    {
        _diagnosticCts?.Cancel();

        if (_lspService == null || _buffer == null || string.IsNullOrEmpty(_currentFilePath)) return;
        if (Directory.Exists(_currentFilePath)) return;

        string ext = Path.GetExtension(_currentFilePath ?? "").ToLower();

        if (ext != ".cs" && ext != ".razor") 
        {
            var emptyList = new List<LspService.MappedDiagnostic>();
            _editor.SetDiagnostics(emptyList);
            _terminal.ShowDiagnostics(emptyList, Path.GetFileName(_currentFilePath ?? ""));
            return;
        }
    
        _diagnosticCts = new CancellationTokenSource();
        var token = _diagnosticCts.Token;

        string code = _buffer.GetAllText();
        string path = _currentFilePath;
        string fileName = Path.GetFileName(path);

        _ = Task.Run(new Func<Task>(async () => 
        {
            try 
            {
                if (!instant) await Task.Delay(600, token); 
                if (token.IsCancellationRequested) return;

                var diagnostics = await _lspService.GetDiagnosticsAsync(code, path);

                if (!token.IsCancellationRequested && diagnostics != null)
                {
                    _editor?.SetDiagnostics(diagnostics);
                    _terminal?.ShowDiagnostics(diagnostics, fileName);
                }
            }
            catch (TaskCanceledException) { /* Ignora */ }
            catch (Exception ex) { Console.WriteLine($"Erro diagnósticos: {ex.Message}"); }
        }), token);
    }
    
    private void ApplyAutocomplete()
    {
        var item = _autocomplete.GetCurrentItem(); 
        if (string.IsNullOrEmpty(item)) return;
        
        if (item.StartsWith("using "))
        {
            string cleanItem = item.Replace("\r", "").Replace("\n", "").Trim();
            _tabManager.ActiveTab!.Buffer.InsertLine(0, cleanItem);            _autocomplete.IsVisible = false;
            RequestDiagnostics(instant: true);
            return;
        }

        var activeTab = _tabManager.ActiveTab;
        if (activeTab == null) return;

        string currentLine = activeTab.Buffer.GetLine(activeTab.Cursor.Line);
        string partialWord = GetPartialWord(currentLine, activeTab.Cursor.Column);

        if (partialWord.Length > 0)
        {
            for(int i = 0; i < partialWord.Length; i++) 
                activeTab.Cursor.MoveLeft();
          
            for(int i=0; i < partialWord.Length; i++) 
                activeTab.Buffer.Delete(activeTab.Cursor.Line, activeTab.Cursor.Column);
        }
        
        string cleanCode = item.Replace("\r", "").Replace("\n", "");
        activeTab.Buffer.Insert(activeTab.Cursor.Line, activeTab.Cursor.Column, cleanCode);
        
        for(int i = 0; i < item.Length; i++) 
            activeTab.Cursor.MoveRight();
       

        _autocomplete.IsVisible = false;
        _window.DoRender(); 
    }
}