using Silk.NET.Core;
using SkiaSharp;
using SLText.Core.Engine;
using SLText.View.Services;
using SLText.View.Styles;

namespace SLText.View.UI;

public partial class WindowManager
{
    private void SetWindowIcon()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.png");
        if (!File.Exists(path)) return;

        using var stream = File.OpenRead(path);
        using var bitmap = SKBitmap.Decode(stream);

        using var rgbaBitmap = new SKBitmap(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul);

        using (var canvas = new SKCanvas(rgbaBitmap))
        {
            canvas.DrawBitmap(bitmap, 0, 0);
        }

        var icon = new RawImage(rgbaBitmap.Width, rgbaBitmap.Height, rgbaBitmap.Bytes);
        _window.SetWindowIcon(new[] { icon });
    }
    
    private void SyncSettings()
    {
        if (_isLoadingSession) return;
        var settings = SettingsService.Load();
        settings.FontSize = _editor.FontSize;
        settings.Theme = _currentTheme.Background.Red < 128 ? "Dark" : "Light";
        settings.LastRootDirectory = _lastDirectory;
    
        settings.OpenTabs = _tabManager.Tabs
            .Where(t => !string.IsNullOrEmpty(t.FilePath))
            .Select(t => t.FilePath!)
            .ToList();

        SettingsService.SaveImmediate(settings);
    }
    
    public void ApplySavedFontSize(float size)
    {
        if (size <= 0) size = 16f;
        _editor.FontSize = size;
        _editor.RequestScrollToCursor();
    }
    
    public void SetCurrentFile(string path, bool resetCursor = false)
    {
        if (string.IsNullOrEmpty(path)) return;

        if (Directory.Exists(path))
        {
            _explorer.SetRootDirectory(path);
            _explorer.ResetScroll();
            _explorer.IsVisible = true;
            _lastDirectory = path;
            _terminal.SetWorkingDirectory(path);
            _runService.ScanProject(path);
            
            Task.Run(() => {
                _lspService.LoadProjectFiles(path, (msg) => _terminal.WriteOutput("Output", msg));
            });
            SyncSettings();
            return; 
        }

        if (File.Exists(path))
        {
            var existingTab = _tabManager.Tabs.FirstOrDefault(t => t.FilePath == path);
            if (existingTab != null)
            {
                _tabManager.SelectTab(_tabManager.Tabs.IndexOf(existingTab));
            }
            else
            {
                if (_tabManager.Tabs.Count == 1 && string.IsNullOrEmpty(_tabManager.Tabs[0].FilePath))
                {
                    var tab = _tabManager.Tabs[0];
                    tab.Buffer.LoadText(File.ReadAllText(path).Replace("\t", "    "));
                    tab.FilePath = path;
                    tab.IsDirty = false;
                }
                else
                {
                    var newBuffer = new TextBuffer();
                    newBuffer.LoadText(File.ReadAllText(path).Replace("\t", "    "));
                    _tabManager.AddTab(newBuffer, new CursorManager(newBuffer), path);
                }
            }
        
            SyncActiveTab(resetCursor);
            SyncSettings();
        }
    }
    
    public void ApplyTheme(EditorTheme theme)
    {
        _currentTheme = theme;
        _editor.SetTheme(theme);
        _statusBar.ApplyTheme(theme);
        _explorer.ApplyTheme(theme);
        _commandPalette.ApplyTheme(theme);
        _tabComponent.ApplyTheme(theme);
        _search.ApplyTheme(theme);
        _terminal.ApplyTheme(theme);
        _signatureHelp.ApplyTheme(theme);
        _autocomplete.ApplyTheme(theme);
    }
    
    private void OnWindowClosing()
    {
        _terminal.ShutdownAllTerminals();
        Console.WriteLine("Aplicação e sub-processos encerrados com sucesso.");
    }
}