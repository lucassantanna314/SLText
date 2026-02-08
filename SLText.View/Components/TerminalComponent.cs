using System.Text.RegularExpressions;
using Silk.NET.Input;
using SkiaSharp;
using SLText.Core.Engine;
using SLText.View.Abstractions;
using SLText.View.Styles;

namespace SLText.View.Components;

public class TerminalInstance
{
    public string Title { get; set; }
    public TerminalService Service { get; } = new();
    public List<string> OutputLines { get; } = new();
    public List<string> CommandHistory { get; } = new();
    public string CurrentInput { get; set; } = "";
    public int HistoryIndex { get; set; } = -1;
    public float ScrollY { get; set; } = 0;
    public DateTime LastDataReceived { get; set; } = DateTime.MinValue;
    public bool InitialCleanupDone { get; set; } = false;
    public bool IsInEscape { get; set; } = false;
    public bool IsInOsc { get; set; } = false;

    public TerminalInstance(string title)
    {
        Title = title;
    }
}

public class TerminalComponent : IComponent
{
    public SKRect Bounds { get; set; }
    public bool IsVisible { get; set; } = false;
    public float Height { get; set; } = 200;
    
    private readonly SKFont _font;
    private EditorTheme _theme = EditorTheme.Dark;
    private const float LineSpacing = 5;
    private const float TabHeight = 35; 
    private const float TabWidth = 140;
    
    private readonly List<TerminalInstance> _terminals = new();
    private int _activeTabIndex = 0;
    private TerminalInstance ActiveTerminal => _terminals[_activeTabIndex];
    
    private bool _cursorVisible = true;
    private double _cursorTimer = 0;
    private bool _isResizing = false;
    public bool IsResizing => _isResizing;
    private float _lastMouseY;
    private bool _isDraggingVertical = false;
    
    private string? _currentWorkingDirectory;
    public void SetWorkingDirectory(string? path) => _currentWorkingDirectory = path;
    
    private static readonly Regex AnsiRegex = new Regex(
        @"\x1B\[[0-9;]*[a-zA-Z]|\x1B\]0;.*?\x07|\x1B\]0;.*?\x1B\\|\x0F", 
        RegexOptions.Compiled);
    
    public TerminalComponent()
    {
        string fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "JetBrainsMono-Regular.ttf");
        var typeface = File.Exists(fontPath) ? SKTypeface.FromFile(fontPath) : SKTypeface.FromFamilyName("monospace");
        _font = new SKFont(typeface, 13);
        
        CreateNewTab("bash", forceNew: true);
    }
    
    public TerminalInstance CreateNewTab(string title, string? workingDirectory = null, bool forceNew = false)
    {
        lock(_terminals)
        {
            if (!forceNew)
            {
                var existing = _terminals.FirstOrDefault(t => t.Title == title);
            
                if (existing != null)
                {
                    _activeTabIndex = _terminals.IndexOf(existing);
                    lock(existing.OutputLines)
                    {
                        existing.OutputLines.Clear();
                        existing.OutputLines.Add("");
                    }
                    existing.Service.Restart(workingDirectory ?? _currentWorkingDirectory);
                    existing.InitialCleanupDone = false;
                    existing.LastDataReceived = DateTime.MinValue; 
                    return existing;
                }
            }
        }

        string finalTitle = title;
    
        if (title == "bash")
        {
            string folderName = !string.IsNullOrEmpty(_currentWorkingDirectory) 
                ? Path.GetFileName(_currentWorkingDirectory.TrimEnd(Path.DirectorySeparatorChar)) 
                : "bash";
        
            int count = _terminals.Count(t => t.Title.Contains(folderName)) + 1;
            finalTitle = $"{folderName} ({count})";
        }
    
        var terminal = new TerminalInstance(finalTitle);
        terminal.Service.OnDataReceived += (data) => ProcessTerminalData(terminal, data);
        terminal.Service.Start(workingDirectory ?? _currentWorkingDirectory);
    
        lock(_terminals)
        {
            _terminals.Add(terminal);
            _activeTabIndex = _terminals.Count - 1;
        }
        return terminal;
    }

    private void ProcessTerminalData(TerminalInstance term, string data)
    {
        term.LastDataReceived = DateTime.Now;
        
        lock (term.OutputLines)
        {
            if (OperatingSystem.IsWindows()) term.InitialCleanupDone = true;
            
            for (int i = 0; i < data.Length; i++)
            {
                char c = data[i];

                if (c == '\x1B')
                {
                    term.IsInEscape = true;
                    if (i + 1 < data.Length && data[i + 1] == ']') term.IsInOsc = true;
                    continue;
                }

                if (term.IsInOsc)
                {
                    if (c == '\x07')
                    {
                        term.IsInOsc = false;
                        term.IsInEscape = false;
                    }

                    continue;
                }

                if (term.IsInEscape)
                {
                    if (char.IsLetter(c)) term.IsInEscape = false;
                    continue;
                }

                if (!term.InitialCleanupDone)
                {
                    if (c == '$' || c == '#')
                    {
                        term.OutputLines.Clear();
                        term.OutputLines.Add("");
                        term.InitialCleanupDone = true;
                    }
                    else continue;
                }

                if (c == '\b' || c == '\x7f' || c == '\x08')
                {
                    if (term.OutputLines.Count > 0 && term.OutputLines[^1].Length > 0)
                    {
                        term.OutputLines[^1] = term.OutputLines[^1][..^1];
                    }

                    continue;
                }

                if (c == '\r') continue;

                if (c == '\n')
                {
                    term.OutputLines.Add("");
                    continue;
                }

                if (c < 32 && c != '\t') continue;

                if (term.OutputLines.Count == 0) term.OutputLines.Add("");
                term.OutputLines[^1] += c;
            }

            if (term == ActiveTerminal)
            {
                AutoScrollToBottom();
            }
        }
    }

    public void HandleKeyDown(string text)
    {
        var term = ActiveTerminal;
        if (text == "\n" || text == "\r")
        {
            string cmd = term.CurrentInput.Trim().ToLower();
            if (cmd == "clear") {
                lock (term.OutputLines) { term.OutputLines.Clear(); term.OutputLines.Add(""); }
                term.Service.SendCommand("\n");
                term.CurrentInput = "";
                return;
            }
            if (!string.IsNullOrWhiteSpace(term.CurrentInput)) term.CommandHistory.Add(term.CurrentInput);
            term.HistoryIndex = -1;
            term.Service.SendCommand("\n"); 
            term.CurrentInput = "";
        }
        else if (text == "Backspace")
        {
            if (term.CurrentInput.Length > 0) {
                term.CurrentInput = term.CurrentInput[..^1];
                term.Service.SendCommand("\x7f"); 
            }
        }
        else if (text.Length == 1) 
        {
            term.CurrentInput += text;
            term.Service.SendCommand(text);
        }
    }

    public void Render(SKCanvas canvas)
    {
        if (!IsVisible || _terminals.Count == 0) return;

        // Fundo
        using var bgPaint = new SKPaint { Color = _theme.Background.WithAlpha(235) };
        canvas.DrawRect(Bounds, bgPaint);

        // Borda superior (Splitter)
        using var borderPaint = new SKPaint { Color = _theme.LineHighlight };
        canvas.DrawLine(Bounds.Left, Bounds.Top, Bounds.Right, Bounds.Top, borderPaint);
        
        RenderTabs(canvas);
        
        var contentBounds = new SKRect(Bounds.Left, Bounds.Top + TabHeight, Bounds.Right, Bounds.Bottom);
        canvas.Save();
        canvas.ClipRect(contentBounds);
        
        float x = Bounds.Left + 10;
        float lineHeight = _font.Size + LineSpacing;
        float startY = contentBounds.Top + 20 - ActiveTerminal.ScrollY;

        using var textPaint = new SKPaint { Color = _theme.Foreground, IsAntialias = true };

        lock (ActiveTerminal.OutputLines)
        {
            for (int i = 0; i < ActiveTerminal.OutputLines.Count; i++)
            {
                float lineY = startY + (i * lineHeight);
                if (lineY < contentBounds.Top - lineHeight || lineY > contentBounds.Bottom + lineHeight) continue;

                string line = ActiveTerminal.OutputLines[i];
                float currentX = x;

                if (line.Contains("@") && line.Contains(":"))
                {
                    textPaint.Color = _theme.Keyword;
                    canvas.DrawText(line, currentX, lineY, _font, textPaint);
                    continue;
                }

                string[] words = line.Split(' ');
                foreach (var word in words)
                {
                    string upperWord = word.ToUpper();
        
                    if (upperWord.Contains("ERROR") || upperWord.Contains("FAIL") || upperWord.Contains("FATAL"))
                        textPaint.Color = SKColors.Salmon; 
                    else if (upperWord.Contains("WARN") || upperWord.Contains("WARNING"))
                        textPaint.Color = SKColors.Khaki; 
                    else if (upperWord.Contains("INFO") || upperWord.Contains("SUCCESS"))
                        textPaint.Color = SKColors.LightGreen; 
                    else if (word.StartsWith("http://") || word.StartsWith("https://") || word.Contains("/"))
                        textPaint.Color = SKColors.SkyBlue; 
                    else if (upperWord.Contains("SHEDULER") || upperWord.Contains("TASK"))
                        textPaint.Color = _theme.Method; 
                    else
                        textPaint.Color = _theme.Foreground; 

                    canvas.DrawText(word + " ", currentX, lineY, _font, textPaint);
                    currentX += _font.MeasureText(word + " ");
                }
            }

            if (_cursorVisible && ActiveTerminal.OutputLines.Count > 0)
            {
                string lastLine = ActiveTerminal.OutputLines[^1];
                float lastLineY = startY + ((ActiveTerminal.OutputLines.Count - 1) * lineHeight);
                float lineWidth = _font.MeasureText(lastLine);
                using var cursorPaint = new SKPaint { Color = _theme.Cursor };
                canvas.DrawRect(x + lineWidth + 2, lastLineY + 2, 8, 2, cursorPaint);
            }
        }
        
        canvas.Restore();

        DrawScrollbar(canvas);
    }
    
    private void DrawScrollbar(SKCanvas canvas)
    {
        if (_terminals.Count == 0) return;
    
        var term = ActiveTerminal;
        float contentHeight = Bounds.Height - TabHeight;
        float totalHeight = term.OutputLines.Count * (_font.Size + LineSpacing) + 20;
    
        if (totalHeight <= contentHeight) return;

        using var scrollPaint = new SKPaint { 
            Color = _theme.LineHighlight.WithAlpha(150),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        float viewRatio = contentHeight / totalHeight;
        float barHeight = Math.Max(20, contentHeight * viewRatio);
        float barY = Bounds.Top + TabHeight + (term.ScrollY / totalHeight) * contentHeight;

        var vBar = new SKRect(Bounds.Right - 7, barY, Bounds.Right - 1, barY + barHeight);
        canvas.DrawRoundRect(vBar, 3, 3, scrollPaint);
    }
    
    private void RenderTabs(SKCanvas canvas)
    {
        float currentX = Bounds.Left;
        using var barBg = new SKPaint { Color = _theme.StatusBarBackground };
        canvas.DrawRect(Bounds.Left, Bounds.Top, Bounds.Width, TabHeight, barBg);

        for (int i = 0; i < _terminals.Count; i++)
        {
            var term = _terminals[i];
            bool isActive = i == _activeTabIndex;
            var tabRect = new SKRect(currentX, Bounds.Top, currentX + TabWidth, Bounds.Top + TabHeight);

            using var tabP = new SKPaint { Color = isActive ? _theme.Background : SKColors.Transparent };
            canvas.DrawRect(tabRect, tabP);

            bool isBusy = (DateTime.Now - term.LastDataReceived).TotalSeconds < 2;

            if (isBusy)
            {
                using var activePaint = new SKPaint 
                { 
                    Color = SKColors.LightGreen.WithAlpha((byte)(_cursorVisible ? 255 : 150)), 
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };
            
                canvas.DrawCircle(tabRect.Left + 10, tabRect.MidY, 3.5f, activePaint);
            }

            using var textP = new SKPaint { 
                Color = isActive ? _theme.Foreground : _theme.Foreground.WithAlpha(150), 
                IsAntialias = true,
                TextSize = _font.Size
            };
    
            string title = term.Title;
            if (title.Length > 10) title = title.Substring(0, 8) + "..";
        
            float textOffsetX = isBusy ? 22 : 12;
            canvas.DrawText(title, tabRect.Left + textOffsetX, tabRect.MidY + 5, _font, textP);

            canvas.DrawText("×", tabRect.Right - 20, tabRect.MidY + 5, _font, textP);

            if (isActive) {
                using var accent = new SKPaint { Color = _theme.TabActiveAccent };
                canvas.DrawRect(tabRect.Left, tabRect.Bottom - 2, TabWidth, 2, accent);
            }
            currentX += TabWidth + 1;
        }

        using var plusP = new SKPaint { Color = _theme.Foreground.WithAlpha(180), IsAntialias = true };
        canvas.DrawText("+", currentX + 10, Bounds.Top + TabHeight/2 + 6, _font, plusP);
    }
    
    private void AutoScrollToBottom() {
        float totalHeight = ActiveTerminal.OutputLines.Count * (_font.Size + LineSpacing);
        ActiveTerminal.ScrollY = Math.Max(0, totalHeight - (Bounds.Height - TabHeight - 30));
    }
    
    public void ApplyScroll(float deltaY) {
        float totalHeight = ActiveTerminal.OutputLines.Count * (_font.Size + LineSpacing) + 20;
        float maxScroll = Math.Max(0, totalHeight - (Bounds.Height - TabHeight));
        ActiveTerminal.ScrollY = Math.Clamp(ActiveTerminal.ScrollY + deltaY, 0, maxScroll);
    }
    
    public void HandleSpecialKey(Key key) {
        var term = ActiveTerminal;
        if (key == Key.Up && term.CommandHistory.Count > 0 && term.HistoryIndex < term.CommandHistory.Count - 1) {
            term.HistoryIndex++;
            ClearCurrentLineAndSet(term.CommandHistory[^(term.HistoryIndex + 1)]);
        }
        else if (key == Key.Down) {
            if (term.HistoryIndex > 0) {
                term.HistoryIndex--;
                ClearCurrentLineAndSet(term.CommandHistory[^(term.HistoryIndex + 1)]);
            } else if (term.HistoryIndex == 0) {
                term.HistoryIndex = -1;
                ClearCurrentLineAndSet("");
            }
        }
    }
    
    private void ClearCurrentLineAndSet(string cmd) {
        while(ActiveTerminal.CurrentInput.Length > 0) HandleKeyDown("Backspace");
        foreach(char c in cmd) HandleKeyDown(c.ToString());
    }
    
    public void OnMouseDown(float x, float y)
    {
        if (Math.Abs(y - Bounds.Top) < 15)
        {
            _isResizing = true;
            _lastMouseY = y;
            return; 
        }
        
        if (y >= Bounds.Top && y <= Bounds.Top + TabHeight)
        {
            float relX = x - Bounds.Left;
        
            for (int i = 0; i < _terminals.Count; i++)
            {
                float tabStartX = i * (TabWidth + 1);
                float tabEndX = tabStartX + TabWidth;

                if (relX >= tabStartX && relX <= tabEndX)
                {
                    if (relX > tabEndX - 25)
                    {
                        CloseTab(i);
                    }
                    else
                    {
                        _activeTabIndex = i;
                    }
                    return;
                }
            }

            float plusBtnStart = _terminals.Count * (TabWidth + 1);
            if (relX > plusBtnStart && relX < plusBtnStart + 40)
            {
                CreateNewTab("bash", forceNew: true);
            }
            return;
        }

        if (Math.Abs(y - Bounds.Top) < 5)
        {
            _isResizing = true;
            _lastMouseY = y;
        }
    }
    
    public void CloseTab(int index)
    {
        if (index < 0 || index >= _terminals.Count) return;

        lock (_terminals)
        {
            try {
                _terminals[index].Service.SendCommand("exit\n");
                _terminals[index].Service.Stop();
            } catch { /* Ignora erros ao fechar */ }

            _terminals.RemoveAt(index);

            if (_activeTabIndex >= _terminals.Count)
            {
                _activeTabIndex = Math.Max(0, _terminals.Count - 1);
            }
        
            if (_terminals.Count == 0)
            {
                //CreateNewTab("bash");
                IsVisible = false;
            }
        }
    }
    
    public void ShutdownAllTerminals()
    {
        lock (_terminals)
        {
            foreach (var terminal in _terminals)
            {
                terminal.Service.Stop();
            }
            _terminals.Clear();
        }
    }
    
    public void InterruptActiveTerminal()
    {
        if (_terminals.Count > 0)
        {
            ActiveTerminal.Service.SendInterrupt();
        }
    }
    
    public void OnMouseMove(float x, float y, float windowHeight)
    {
        if (_isResizing)
        {
            float deltaY = _lastMouseY - y; 
            Height = Math.Clamp(Height + deltaY, 100, windowHeight - 200);
            _lastMouseY = y;
            return;
        }
        
        if (_isDraggingVertical && _terminals.Count > 0)
        {
            var term = ActiveTerminal;
            lock (term.OutputLines)
            {
                float totalHeight = term.OutputLines.Count * (_font.Size + LineSpacing) + 20;
                float deltaY = y - _lastMouseY;
                float contentHeight = Bounds.Height - TabHeight;
                float scrollDelta = (deltaY / contentHeight) * totalHeight;
            
                ApplyScroll(scrollDelta);
                _lastMouseY = y;
            }
        }
    }
    public void OnMouseUp() 
    {
        _isResizing = false;
        _isDraggingVertical = false;
    }
    
    public void ApplyTheme(EditorTheme theme) => _theme = theme;
    
    public void Update(double deltaTime)
    {
        _cursorTimer += deltaTime;
        if (_cursorTimer > 0.5) { _cursorVisible = !_cursorVisible; _cursorTimer = 0; }
    }
}