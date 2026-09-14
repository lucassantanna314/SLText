using SkiaSharp;

using SLText.View.Services;

namespace SLText.View.UI;

public partial class WindowManager
{
     private void OnRender(double dt)
     {
        // The surface is recreated on resize and can legitimately be absent for a frame. Skipping
        // the frame beats a NullReferenceException on the render callback, which kills the process.
        if (_surface == null) return;

        if (!_firstFrameLogged)
        {
            _firstFrameLogged = true;
            StartupLog.Write("OnRender: FIRST FRAME rendering", $"window={_window.Size.X}x{_window.Size.Y}");
        }

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

            // The render loop must not drive state transitions: SyncActiveTab owns _currentFilePath,
            // the syntax rules and the window title. Re-deriving them here meant a full re-highlight
            // could be triggered from the render thread, racing with the input thread.
            if (!ReferenceEquals(_renderedTab, active))
            {
                _renderedTab = active;
                _editor.SetCurrentData(active.Buffer, active.Cursor);
            }

            if (_terminal.IsVisible)
            {
                _terminal.Bounds = new SKRect(explorerWidth, height - footerHeight - terminalHeight, width, height - footerHeight);
                _terminal.Render(canvas);
            }

            float editorBottom = height - footerHeight - terminalHeight;

            _editor.Bounds = new SKRect(explorerWidth, tabHeight, width, editorBottom);
            _editor.Render(canvas);
        }
        else
        {
            _renderedTab = null;
        }

        _statusBar.Bounds = new SKRect(0, height - footerHeight, width, height);
        string fileInfo = string.IsNullOrEmpty(_currentFilePath) ? "New File" : Path.GetFileName(_currentFilePath);
        if (_statusBar.FileInfo != fileInfo) _statusBar.FileInfo = fileInfo;
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

        if (_contextMenu.IsVisible)
        {
            _contextMenu.Render(canvas);
        }

        _grContext.Flush();
    }
}