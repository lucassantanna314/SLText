using SLText.Core.Engine.LSP;

namespace SLText.View.UI;

public partial class WindowManager
{
    private const int DiagnosticDebounceMs = 600;
    private const int CompletionDebounceMs = 80;
    private const int MaxCompletionItems = 200;

    private CancellationTokenSource? _completionCts;
    private CancellationTokenSource? _signatureCts;

    private static bool IsAnalyzableFile(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".cs" or ".razor";
    }

    /// <summary>
    /// Queues a diagnostics pass for the active file.
    /// </summary>
    /// <remarks>
    /// Roslyn results are computed on a background thread but must be applied on the window thread:
    /// the render loop enumerates the diagnostic list, so handing it a list from another thread can
    /// paint squiggles belonging to a tab the user has already left. Results are therefore marshalled
    /// through <see cref="_pendingAction"/> and dropped if the active tab changed meanwhile.
    /// </remarks>
    private void RequestDiagnostics(bool instant = false)
    {
        _diagnosticCts?.Cancel();
        _diagnosticCts?.Dispose();
        _diagnosticCts = null;

        if (_lspService == null || _tabManager.ActiveTab == null) return;

        var tab = _tabManager.ActiveTab;
        string path = tab.FilePath ?? string.Empty;

        if (!IsAnalyzableFile(path))
        {
            _pendingAction = () =>
            {
                var empty = new List<LspService.MappedDiagnostic>();
                _editor.SetDiagnostics(empty);
                _terminal.ShowDiagnostics(empty, tab.Title);
            };
            return;
        }

        _diagnosticCts = new CancellationTokenSource();
        var token = _diagnosticCts.Token;

        string code = tab.Buffer.GetAllText();
        string fileName = Path.GetFileName(path);
        string tabId = tab.Id;

        _ = Task.Run(async () =>
        {
            try
            {
                if (!instant) await Task.Delay(DiagnosticDebounceMs, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;

                var diagnostics = await _lspService.GetDiagnosticsAsync(code, path).ConfigureAwait(false);
                if (token.IsCancellationRequested || diagnostics == null) return;

                _pendingAction = () =>
                {
                    if (_tabManager.ActiveTab?.Id != tabId) return; // stale: user switched tabs
                    _editor.SetDiagnostics(diagnostics);
                    _terminal.ShowDiagnostics(diagnostics, fileName);
                };
            }
            catch (OperationCanceledException) { /* superseded by a newer request */ }
            catch (ObjectDisposedException) { /* cancelled during shutdown */ }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SLText] Diagnostics failed: {ex.Message}");
            }
        }, token);
    }

    /// <summary>
    /// Queues a completion request for the caret position captured at call time.
    /// </summary>
    /// <remarks>
    /// The previous implementation awaited Roslyn inline in the GLFW char callback with no debounce
    /// and no cancellation, then applied the result using state re-read after the await - so fast
    /// typing produced overlapping requests whose results landed on whichever tab was active last.
    /// </remarks>
    private void RequestCompletions()
    {
        _completionCts?.Cancel();
        _completionCts?.Dispose();
        _completionCts = null;

        var tab = _tabManager.ActiveTab;
        if (tab == null || !IsAnalyzableFile(tab.FilePath))
        {
            _autocomplete.IsVisible = false;
            return;
        }

        _completionCts = new CancellationTokenSource();
        var token = _completionCts.Token;

        // Snapshot everything the request needs on the calling thread.
        string code = tab.Buffer.GetAllText();
        string path = tab.FilePath!;
        int line = tab.Cursor.Line;
        int column = tab.Cursor.Column;
        int offset = tab.Buffer.GetFlatOffset(line, column);
        string partialWord = GetPartialWord(tab.Buffer.GetLine(line), column);
        var position = _editor.GetCursorScreenPosition();
        string tabId = tab.Id;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(CompletionDebounceMs, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;

                var completions = await _lspService.GetCompletionsAsync(code, offset, path).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;

                var filtered = completions
                    .Select(i => i.DisplayText)
                    .Where(text => partialWord.Length == 0 ||
                                   text.StartsWith(partialWord, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.Ordinal)
                    .Take(MaxCompletionItems)
                    .ToList();

                _pendingAction = () =>
                {
                    if (_tabManager.ActiveTab?.Id != tabId) return;
                    if (filtered.Count > 0) _autocomplete.Show(position.x, position.y + 20, filtered);
                    else _autocomplete.IsVisible = false;
                };
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SLText] Completion failed: {ex.Message}");
            }
        }, token);
    }

    /// <summary>Shows or refreshes signature help for the caret position.</summary>
    private void RequestSignatureHelp()
    {
        _signatureCts?.Cancel();
        _signatureCts?.Dispose();
        _signatureCts = null;

        var tab = _tabManager.ActiveTab;
        if (tab == null || !IsAnalyzableFile(tab.FilePath))
        {
            _signatureHelp.IsVisible = false;
            return;
        }

        _signatureCts = new CancellationTokenSource();
        var token = _signatureCts.Token;

        string code = tab.Buffer.GetAllText();
        string path = tab.FilePath!;
        int offset = tab.Buffer.GetFlatOffset(tab.Cursor.Line, tab.Cursor.Column);
        var position = _editor.GetCursorScreenPosition();
        string tabId = tab.Id;

        _ = Task.Run(async () =>
        {
            try
            {
                if (token.IsCancellationRequested) return;

                var result = await _lspService.GetSignatureHelpAsync(code, offset, path).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;

                _pendingAction = () =>
                {
                    if (_tabManager.ActiveTab?.Id != tabId) return;
                    if (result != null && result.Signatures.Count > 0) _signatureHelp.Show(position.x, position.y, result);
                    else _signatureHelp.IsVisible = false;
                };
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SLText] Signature help failed: {ex.Message}");
            }
        }, token);
    }

    private void ApplyAutocomplete()
    {
        var item = _autocomplete.GetCurrentItem();
        var activeTab = _tabManager.ActiveTab;
        if (string.IsNullOrEmpty(item) || activeTab == null) return;

        _autocomplete.IsVisible = false;

        if (item.StartsWith("using "))
        {
            string usingLine = item.Replace("\r", "").Replace("\n", "").Trim();
            activeTab.Buffer.InsertLine(0, usingLine);
            RequestDiagnostics(instant: true);
            return;
        }

        string currentLine = activeTab.Buffer.GetLine(activeTab.Cursor.Line);
        string partialWord = GetPartialWord(currentLine, activeTab.Cursor.Column);

        if (partialWord.Length > 0)
        {
            for (int i = 0; i < partialWord.Length; i++) activeTab.Cursor.MoveLeft();
            for (int i = 0; i < partialWord.Length; i++) activeTab.Buffer.Delete(activeTab.Cursor.Line, activeTab.Cursor.Column);
        }

        // Newlines are stripped before inserting, so the caret must advance by the inserted length.
        // Advancing by item.Length instead pushed the caret past the end of the line whenever a
        // multi-line snippet was accepted.
        string textToInsert = item.Replace("\r", "").Replace("\n", "");
        activeTab.Buffer.Insert(activeTab.Cursor.Line, activeTab.Cursor.Column, textToInsert);

        for (int i = 0; i < textToInsert.Length; i++) activeTab.Cursor.MoveRight();

        activeTab.IsDirty = true;
        UpdateTitle();
        _window.DoRender();
    }
}
