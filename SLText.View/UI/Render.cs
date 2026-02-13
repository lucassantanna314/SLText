using SkiaSharp;

namespace SLText.View.UI;

public partial class WindowManager
{
     private void OnRender(double dt)
     {
        var canvas = _surface.Canvas;
        canvas.Clear(_currentTheme.Background);

        float width = _window.Size.X;
        float height = _window.Size.Y;
        float footerHeight = 25;
        float terminalHeight = _terminal.IsVisible ? _terminal.Height : 0;
        float explorerWidth = _explorer.IsVisible ? _explorer.Width : 0;
        float tabHeight = _tabComponent.GetRequiredHeight();

        _explorer.Bounds = new SKRect(0, 0, explorerWidth, height - footerHeight);
        _explorer.Render(canvas);

        _tabComponent.Bounds = new SKRect(explorerWidth, 0, width, tabHeight);
        _tabComponent.Render(canvas);

        if (_tabManager.ActiveTab != null)
        {
            var active = _tabManager.ActiveTab;

            if (_currentFilePath != active.FilePath)
            {
                _currentFilePath = active.FilePath;
                _editor.UpdateSyntax(_currentFilePath);
                UpdateTitle();
            }

            if (_terminal.IsVisible)
            {
                _terminal.Bounds = new SKRect(explorerWidth, height - footerHeight - terminalHeight, width, height - footerHeight);
                _terminal.Render(canvas);
            }

            float editorBottom = height - footerHeight - terminalHeight;

            _editor.Bounds = new SKRect(explorerWidth, tabHeight, width, editorBottom);
            _editor.SetCurrentData(active.Buffer, active.Cursor);
            _editor.Render(canvas);
        }

        _statusBar.Bounds = new SKRect(0, height - footerHeight, width, height);
        _statusBar.FileInfo = string.IsNullOrEmpty(_currentFilePath) ? "New File" : Path.GetFileName(_currentFilePath);
        _statusBar.Render(canvas);

        if (_modal.IsVisible)
        {
            _modal.Render(canvas, new SKRect(0, 0, width, height), _currentTheme);
        }

        if (_search.IsVisible)
        {
            _search.Render(canvas, new SKRect(0, 0, _window.Size.X, _window.Size.Y), _currentTheme);
        }

        if (_commandPalette.IsVisible)
        {
            _commandPalette.Bounds = new SKRect(0, 0, width, height);
            _commandPalette.ApplyTheme(_currentTheme);
            _commandPalette.Render(canvas);
        }
        
        if (_autocomplete.IsVisible)
        {
            _autocomplete.Render(canvas, _currentTheme);
        }
        
        if (_signatureHelp.IsVisible)
        {
            _signatureHelp.Render(canvas, _currentTheme);
        }

        _grContext.Flush();
    }
}