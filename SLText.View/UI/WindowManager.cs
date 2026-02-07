using Silk.NET.Input;
using Silk.NET.Windowing;
using SkiaSharp;
using SLText.Core.Engine;
using SLText.View.Components;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using SLText.View.Services;
using SLText.View.Styles;
using SLText.View.UI.Input;
using TextCopy;

namespace SLText.View.UI;

public class WindowManager : IDisposable
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private IWindow _window;
    private GRContext _grContext = null!;
    private SKSurface _surface = null!;
    
    private EditorComponent _editor;
    private StatusBarComponent _statusBar;
    
    private InputHandler _inputHandler;
    
    private TextBuffer _buffer;
    private CursorManager _cursor;
    private string? _currentFilePath;
    private bool _isDirty;
    public bool IsDirty => _isDirty;
    private bool _isMouseDown;
    private Action? _pendingAction;
    
    private EditorTheme _currentTheme = EditorTheme.Dark;

    private MouseHandler _mouseHandler;
    private ModalComponent _modal = new();
    private SearchComponent _search = new();
    
    private TabManager _tabManager = new();
    private TabComponent _tabComponent;
    private bool _isResizingExplorer = false;
    private FileExplorerComponent _explorer = new();
    private CommandPaletteComponent _commandPalette = new();
    private TerminalComponent _terminal;
    private bool _isTerminalFocused = false;
    private IMouse? _primaryMouse;

    private void ApplyTheme(EditorTheme theme)
    {
        _currentTheme = theme;
        _editor.SetTheme(theme);
        _statusBar.ApplyTheme(theme);
        _explorer.ApplyTheme(theme);
        _commandPalette.ApplyTheme(theme);
        _tabComponent.ApplyTheme(theme);
        _search.ApplyTheme(theme);
        _terminal.ApplyTheme(theme);
    }

    public WindowManager(TextBuffer buffer, CursorManager cursor, InputHandler input, string? initialFilePath = null)
    {
        var options = WindowOptions.Default;
        options.Size = new Silk.NET.Maths.Vector2D<int>(800, 600);
        options.Title = "SLText";
        _window = Window.Create(options);
        _window.Closing += OnWindowClosing;
        
        
        _buffer = buffer;
        _cursor = cursor;
        _inputHandler = input;
        
        // 2. componentes visuais
        _editor = new EditorComponent(buffer, cursor);
        _inputHandler.AddEditorShortcuts(_editor);
        
        _statusBar = new StatusBarComponent(cursor, buffer, _editor);
        
        _tabManager = new TabManager();
        _tabComponent = new TabComponent(_tabManager);
        _tabManager.AddTab(buffer, cursor, initialFilePath);
        _editor = new EditorComponent(_tabManager.ActiveTab!.Buffer, _tabManager.ActiveTab.Cursor);
        _terminal = new TerminalComponent();
        
        _currentFilePath = initialFilePath;
        
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Update += OnUpdate;
        _window.FramebufferResize += OnResize;
        
        if (input.GetDialogService() is NativeDialogService nativeDialog)
        {
            nativeDialog.Modal = _modal;
        }
        
        _inputHandler.OnTabCloseRequested += () => CloseActiveTab();

        _inputHandler.OnNextTabRequested += () => {
            _tabManager.NextTab();
            SyncActiveTab(false);
        };

        _inputHandler.OnPreviousTabRequested += () => {
            _tabManager.PreviousTab();
            SyncActiveTab(false);
        };
        
        _inputHandler.OnToggleExplorerRequested += () => {
            _explorer.IsVisible = !_explorer.IsVisible;
        };
        
        _inputHandler.OnOpenFolderRequested += () => {
            if (_inputHandler.GetDialogService() is NativeDialogService dialogs) 
            {
                string? folder = dialogs.OpenFolder(_inputHandler.GetLastDirectory()); 
        
                if (!string.IsNullOrEmpty(folder)) 
                {
                    _explorer.SetRootDirectory(folder);
                    _explorer.IsVisible = true;
                    _terminal.SetWorkingDirectory(folder);
                }
            }
        };
        
        _inputHandler.OnFocusExplorerSearchRequested += () => {
            _explorer.IsVisible = true;
            _explorer.IsFocused = true;
        };
        
        _inputHandler.OnThemeToggleRequested += () => 
        {
            if (_currentTheme.Background.Red < 128) 
                ApplyTheme(EditorTheme.Light);
            else 
                ApplyTheme(EditorTheme.Dark);
        };

        _inputHandler.OnNewTerminalTabRequested += () => {
            if (!_terminal.IsVisible) _terminal.IsVisible = true;
    
            _isTerminalFocused = true;
            _terminal.CreateNewTab("bash");
        };
        
        _inputHandler.OnTerminalInterruptRequested += () => {
            if (_isTerminalFocused && _terminal.IsVisible)
            {
                _terminal.InterruptActiveTerminal();
            }
            else if (!_isTerminalFocused)
            {
                _inputHandler.HandleCopy();
            }
        };
    }
    
    private void OnWindowClosing()
    {
        _terminal.ShutdownAllTerminals();
        Console.WriteLine("Aplicação e sub-processos encerrados com sucesso.");
    }
    
    public void OpenSearch() 
    {
        _search.IsVisible = true;
        _search.Clear(); 
    }
    
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

    private void OnLoad()
    {
        // Inicializa Skia com o contexto da GPU da Silk.NET
        var interface_ = GRGlInterface.Create();
        _grContext = GRContext.CreateGl(interface_);
        SetupSurface();
        SetWindowIcon();

        // Configura Input da Silk.NET
        var input = _window.CreateInput();
        _primaryMouse = input.Mice[0];
        
        foreach (var keyboard in input.Keyboards)
        {
            keyboard.KeyDown += OnKeyDown;
            
            keyboard.KeyChar += (k, c) => 
            {
                if (_commandPalette.IsVisible)
                {
                    _commandPalette.HandleInput(c.ToString(), false);
                    return;
                }
                
                if (_explorer.IsFocused) {
                    _explorer.HandleSearchInput(c.ToString(), false);
                    return;
                }
                
                var activeTab = _tabManager.ActiveTab;
                if (activeTab == null) return;

                if (_search.IsVisible)
                {
                    _search.HandleInput(c.ToString(), false);
                    _editor.PerformSearch(_search.SearchText);

                    var firstMatch = activeTab.Buffer.FindNext(_search.SearchText, activeTab.Cursor.Line, activeTab.Cursor.Column);
        
                    if (firstMatch.HasValue)
                    {
                        activeTab.Cursor.SetSelection(firstMatch.Value.line, firstMatch.Value.col, 
                            firstMatch.Value.line, firstMatch.Value.col + _search.SearchText.Length);
                    }
                    return;
                }
    
                if (_modal.IsVisible || _modal.IsRecentlyClosed) return;
    
                bool ctrl = k.IsKeyPressed(Key.ControlLeft) || k.IsKeyPressed(Key.ControlRight);
                if (ctrl) return;
                
                if (_isTerminalFocused && _terminal.IsVisible)
                {
                    _terminal.HandleKeyDown(c.ToString());
                    return;
                }
    
                _inputHandler.HandleTextInput(c);
    
                if (!activeTab.IsDirty) { activeTab.IsDirty = true; UpdateTitle(); }
            };
        }
        
        _mouseHandler = new MouseHandler(_editor, _cursor, _inputHandler, input, _modal);
        
        foreach (var mouse in input.Mice)
        {
            mouse.MouseDown += (m, button) =>
            {
                var pos = m.Position; 
                float explorerWidth = _explorer.IsVisible ? _explorer.Width : 0;
              
                if (_terminal.IsVisible && _terminal.Bounds.Contains(pos.X, pos.Y))
                {
                    _isTerminalFocused = true; 
                    _explorer.IsFocused = false;
                    _terminal.OnMouseDown(pos.X, pos.Y);
                    return;
                }
                
                if (_terminal.IsVisible && Math.Abs(pos.Y - _terminal.Bounds.Top) < 15 && pos.X > explorerWidth)
                {
                    _terminal.OnMouseDown(pos.X, pos.Y); 
                    return;
                }
                
                if (_editor.Bounds.Contains(pos.X, pos.Y) || _explorer.Bounds.Contains(pos.X, pos.Y))
                {
                    _isTerminalFocused = false;
                }

                if (_explorer.IsVisible && _explorer.IsOnResizeBorder(pos.X))
                {
                    _isResizingExplorer = true;
                    return;
                }

                if (_explorer.IsVisible && _explorer.Bounds.Contains(pos.X, pos.Y))
                {
                    if (pos.Y < _explorer.Bounds.Top + 40)
                    {
                        _explorer.IsFocused = true;
                    }
                    else
                    {
                        _explorer.IsFocused = false;
                        _explorer.OnMouseDown(pos.X, pos.Y); 
                    }

                    return;
                }

                if (_explorer.IsFocused) 
                {
                    _explorer.ClearSearch();
                    _explorer.IsFocused = false;
                }

                int clickedIndex = _tabComponent.GetTabIndexAt(pos.X, pos.Y);
                if (clickedIndex != -1)
                {
                    float relativeX = (pos.X - _tabComponent.Bounds.Left) % (130 + 2);

                    if (relativeX > 100)
                    {
                        _tabManager.SelectTab(clickedIndex);
                        CloseActiveTab();
                        return;
                    }

                    if (_tabManager.ActiveTab != null)
                    {
                        _tabManager.ActiveTab.SavedScrollX = _editor.ScrollX;
                        _tabManager.ActiveTab.SavedScrollY = _editor.ScrollY;
                    }

                    _tabManager.SelectTab(clickedIndex);
                    SyncActiveTab(false);
                    return;
                }
                
                if (_editor.Bounds.Contains(pos.X, pos.Y))
                {
                    if (_editor.OnMouseDown(pos.X, pos.Y)) return;
                    _mouseHandler.OnMouseDown(pos.X, pos.Y, button);
                }
            };

            mouse.MouseMove += (m, pos) => 
            {
                
                if (_isResizingExplorer)
                {
                    _explorer.Width = Math.Clamp(pos.X, 100, 500);
                    return;
                }
                
                if (_terminal.IsResizing)
                {
                    _terminal.OnMouseMove(pos.X, pos.Y, _window.Size.Y);
                    foreach (var mouse in input.Mice)
                    {
                        mouse.Cursor.StandardCursor = StandardCursor.VResize;
                    }
                    return;
                }
                else 
                {
                    foreach (var mouse in input.Mice)
                    {
                        mouse.Cursor.StandardCursor = StandardCursor.Default;
                    }
                }
                
                if (_terminal.IsVisible)
                {
                    _terminal.OnMouseMove(pos.X, pos.Y, _window.Size.Y);
                }
                
                _editor.OnMouseMove(pos.X, pos.Y);
                
                if (_explorer.IsVisible)
                {
                    _explorer.OnMouseMove(pos.X, pos.Y);
                }
                _mouseHandler.OnMouseMove(m, pos);
            };
            
            mouse.MouseUp += (m, button) => 
            {
                var pos = m.Position;
                
                _isResizingExplorer = false;

                if (_explorer.IsVisible && _explorer.Bounds.Contains(pos.X, pos.Y))
                {
                    if (!_explorer.WasDragging)
                    {
                        var node = _explorer.GetNodeAt(pos.X, pos.Y);
                        if (node != null)
                        {
                            if (node.IsDirectory)
                            {
                                node.IsExpanded = !node.IsExpanded;
                                if (!node.IsExpanded) _explorer.OnFolderCollapsed();
                                if (node.IsExpanded && node.Children.Count == 0) _explorer.LoadSubNodes(node);
                            }
                            else
                            {
                                SetCurrentFile(node.FullPath);
                            }
                        }
                    }
                    _explorer.OnMouseUp();
                    return;
                }
                
                _editor.OnMouseUp();
                _terminal.OnMouseUp();
                _explorer.OnMouseUp();
                _mouseHandler.OnMouseUp(m, button);
            };
            
            mouse.Scroll += (m, scroll) => 
            {
                var pos = m.Position;
                
                bool isShiftPressed = false;
                foreach (var kbd in input.Keyboards)
                {
                    if (kbd.IsKeyPressed(Key.ShiftLeft) || kbd.IsKeyPressed(Key.ShiftRight))
                    {
                        isShiftPressed = true;
                        break;
                    }
                }
                
                if (_terminal.IsVisible && _terminal.Bounds.Contains(pos.X, pos.Y))
                {
                    _terminal.ApplyScroll(-scroll.Y * 25f);
                    return;
                }
                
                if (_explorer.IsVisible && _explorer.Bounds.Contains(pos.X, pos.Y))
                {
                    float scrollSpeed = 25f;
        
                    if (isShiftPressed)
                    {
                        _explorer.ApplyScroll(-scroll.Y * scrollSpeed, 0);
                    }
                    else
                    {
                        _explorer.ApplyScroll(0, -scroll.Y * scrollSpeed);
                    }
                }
        
                else if (_tabComponent.Bounds.Contains(pos.X, pos.Y))
                {
                    _tabComponent.ApplyScroll(-scroll.Y * 25);
                }
                else
                {
                    _mouseHandler.OnMouseScroll(m, scroll);
                }
            };
        }
        
        _window.FocusChanged += (isFocused) => 
        {
            if (isFocused)
            {
                _inputHandler.ResetTypingState();
            }
        };
        
        _inputHandler.OnScrollRequested += (deltaX, deltaY) => 
        {
            _editor.ApplyScroll(deltaX, deltaY);
        };
        
        _inputHandler.OnZoomRequested += (delta) => 
        {
            _editor.FontSize += delta;
            _editor.RequestScrollToCursor(); 
        };
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var win32 = _window.Native?.Win32;
            if (win32.HasValue)
            {
                var handle = win32.Value.Hwnd;
                int useDarkMode = 1;
                DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
            }
        }
        
        if (!string.IsNullOrEmpty(_currentFilePath) && File.Exists(_currentFilePath))
        {
            try 
            {
                string content = File.ReadAllText(_currentFilePath).Replace("\t", "    ");
                _buffer.LoadText(content);
                _cursor.SetPosition(0, 0);
                _inputHandler.ResetTypingState();
                _statusBar.LanguageName = _editor.UpdateSyntax(_currentFilePath);
                UpdateTitle(); 
            }
            catch (Exception ex)
            {
                _currentFilePath = null;
                _cursor.SetPosition(0, 0);
            }
        }
        else 
        {
            _cursor.SetPosition(0, 0);
            UpdateTitle();
        }
        
        ApplyTheme(EditorTheme.Dark);
    }
    
    private void UpdateTitle()
    {
        var active = _tabManager.ActiveTab;
        if (active == null) return;

        string fileName = string.IsNullOrEmpty(active.FilePath) 
            ? "New File" 
            : Path.GetFileName(active.FilePath);

        string dirtyFlag = active.IsDirty ? "*" : "";

        _window.Title = $"SLText - {fileName}{dirtyFlag}";
    }

    private void OnKeyDown(IKeyboard k, Key key, int arg3)
    {

        bool ctrl = k.IsKeyPressed(Key.ControlLeft) || k.IsKeyPressed(Key.ControlRight);
        bool shift = k.IsKeyPressed(Key.ShiftLeft) || k.IsKeyPressed(Key.ShiftRight);

        if (_isTerminalFocused && _terminal.IsVisible)
        {
            if (key == Key.Up || key == Key.Down)
            {
                _terminal.HandleSpecialKey(key);
                return;
            }
            
            if (key == Key.Escape)
            {
                _isTerminalFocused = false;
                return;
            }

            if (key == Key.Enter)
            {
                _terminal.HandleKeyDown("\n");
                return;
            }

            if (key == Key.Backspace)
            {
                _terminal.HandleKeyDown("Backspace");
                return;
            }

            if (IsNavigationOnly(key)) return; 
        
            if (!ctrl) return; 
        }
        
        if (_commandPalette.IsVisible)
        {
            if (key == Key.Escape) { _commandPalette.IsVisible = false; return; }
            if (key == Key.Up) { _commandPalette.MoveSelection(-1); return; }
            if (key == Key.Down) { _commandPalette.MoveSelection(1); return; }
            if (key == Key.Backspace) { _commandPalette.HandleInput("", true); return; }
            if (key == Key.Enter)
            {
                var cmd = _commandPalette.GetSelectedCommand();
                if (cmd != null)
                {
                    _commandPalette.IsVisible = false;
                    cmd.Action.Invoke();
                }
                return;
            }
        }
        
        if (ctrl && shift && key == Key.P)
        {
            _commandPalette.IsVisible = true;
            _commandPalette.LoadCommands(_inputHandler.GetRegisteredCommands());
            return;
        }
        
        if (_explorer.IsFocused)
        {
            if (key == Key.Escape)
            {
                _explorer.IsFocused = false;
                _explorer.ClearSearch();
            }

            if (key == Key.Backspace) 
            { 
                _explorer.HandleSearchInput("", true); 
                return; 
            }
            return;
        }
        
        if (_search.IsVisible)
        {
            if (key == Key.Escape) 
            { 
                _search.IsVisible = false; 
                _editor.PerformSearch(""); 
                _cursor.ClearSelection(); 
                return; 
            }

            if (key == Key.Backspace) 
            {
                _search.HandleInput("", true); 

                _editor.PerformSearch(_search.SearchText); 

                var searchResult = _buffer.FindNext(_search.SearchText, _cursor.Line, 0); 
                if (searchResult.HasValue)
                {
                    _cursor.SetSelection(searchResult.Value.line, searchResult.Value.col, 
                        searchResult.Value.line, searchResult.Value.col + _search.SearchText.Length);
                }
                return;
            }

            if (key == Key.Enter)
            {
                // Procura a partir da posição atual do cursor + 1
                var nextResult = _buffer.FindNext(_search.SearchText, _cursor.Line, _cursor.Column + 1);
    
                if (nextResult.HasValue)
                {
                    _cursor.SetSelection(nextResult.Value.line, nextResult.Value.col, 
                        nextResult.Value.line, nextResult.Value.col + _search.SearchText.Length);
        
                    _editor.RequestScrollToCursor();
                }
                else 
                {
                    var firstResult = _buffer.FindNext(_search.SearchText, 0, 0);
                    if (firstResult.HasValue)
                    {
                        _cursor.SetSelection(firstResult.Value.line, firstResult.Value.col, 
                            firstResult.Value.line, firstResult.Value.col + _search.SearchText.Length);
                        _editor.RequestScrollToCursor();
                    }
                }
            }
            
            return; 
        }
        
        string? mappedKey = KeyboardMapper.Normalize(key);
        if (mappedKey == null) return;

        if (_modal.IsVisible)
        {
            if (_modal.HandleKeyDown(mappedKey)) return;
        }
        
        if (ctrl && key == Key.C) { _inputHandler.HandleCopy(); return; }
        if (ctrl && key == Key.V) 
        {
            string? text = ClipboardService.GetText();
            if (!string.IsNullOrEmpty(text)) 
            {
                _inputHandler.HandlePaste(text);
                _isDirty = true;
                UpdateTitle();
            }
            return;
        }

        _inputHandler.HandleShortcut(ctrl, shift, mappedKey);

        _editor.RequestScrollToCursor();
    
        if (!IsNavigationOnly(key) && !ctrl && !_isDirty) 
        {
            _isDirty = true;
            UpdateTitle();
        }
    }

    private bool IsNavigationOnly(Key key) => 
        key is Key.Up or Key.Down or Key.Left or Key.Right or Key.PageUp or Key.PageDown or Key.Home or Key.End;

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

        _grContext.Flush();
    }
    
    public void OnSaveSuccess(string? path)
    {
        var active = _tabManager.ActiveTab;
        if (active == null || path == null) return;

        active.FilePath = path;
        active.IsDirty = false;
        _currentFilePath = path;
    
        _statusBar.LanguageName = _editor.UpdateSyntax(path);
        UpdateTitle();
    }

    public void SetCurrentFile(string path, bool resetCursor = false)
    {
        if (string.IsNullOrEmpty(path))
        {
            var newBuf = new TextBuffer();
            var newCur = new CursorManager(newBuf);
            _tabManager.AddTab(newBuf, newCur, null);
        }
        else
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
                    string content = File.ReadAllText(path).Replace("\t", "    ");
                    tab.Buffer.LoadText(content);
                    tab.FilePath = path;
                    tab.IsDirty = false;
                }
                else
                {
                    var newBuffer = new TextBuffer();
                    var newCursor = new CursorManager(newBuffer);
                    string content = File.ReadAllText(path).Replace("\t", "    ");
                    newBuffer.LoadText(content);
                    _tabManager.AddTab(newBuffer, newCursor, path);
                }
            }
            
            if (!_explorer.HasRoot) 
            {
                string? dir = Path.GetDirectoryName(path);
                if (dir != null) _explorer.SetRootDirectory(dir);
            }

            _inputHandler.UpdateLastDirectory(path);
        }

        SyncActiveTab(resetCursor);
    }

    private void SyncActiveTab(bool resetCursor)
    {
        var active = _tabManager.ActiveTab!;
        _currentFilePath = active.FilePath;
        _explorer.SetSelectedFile(active.FilePath);

        _editor.SetCurrentData(active.Buffer, active.Cursor);
        _inputHandler.UpdateActiveData(active.Cursor, active.Buffer);
        _mouseHandler.UpdateActiveCursor(active.Cursor);
        _statusBar.UpdateActiveBuffer(active.Buffer, active.Cursor);

        _statusBar.LanguageName = _editor.UpdateSyntax(_currentFilePath);
        _statusBar.FileInfo = active.Title;

        _inputHandler.UpdateCurrentPath(_currentFilePath);
        if (resetCursor) active.Cursor.SetPosition(0, 0);

        _tabComponent.EnsureActiveTabVisible();

        UpdateTitle();
    }
    
    private StandardCursor _lastAppliedCursor = StandardCursor.Default;
    
    private void OnUpdate(double dt)
    {
        _editor.Update(dt);
        if (_terminal.IsVisible) _terminal.Update(dt);
        
        if (_primaryMouse == null) return;
        var pos = _primaryMouse.Position;
        float mx = pos.X;
        float my = pos.Y;
        
        StandardCursor targetCursor = StandardCursor.Default;
        
        float explorerWidth = _explorer.IsVisible ? _explorer.Width : 0;

        bool isOverTerminalSplitter = _terminal.IsVisible && 
                                      Math.Abs(my - _terminal.Bounds.Top) < 15 && 
                                      mx > explorerWidth;

        bool isOverExplorerSplitter = _explorer.IsVisible && 
                                      _explorer.IsOnResizeBorder(mx) && 
                                      (!_terminal.IsVisible || my < _terminal.Bounds.Top);

        if (_terminal.IsResizing || isOverTerminalSplitter)
        {
            targetCursor = StandardCursor.VResize;
        }
        else if (_isResizingExplorer || isOverExplorerSplitter)
        {
            targetCursor = StandardCursor.HResize;
        }
        
        else if (_explorer.IsVisible && _explorer.Bounds.Contains(mx, my))
        {
            targetCursor = StandardCursor.Default;
        }
        
        if (targetCursor != _lastAppliedCursor)
        {
            _primaryMouse.Cursor.StandardCursor = targetCursor;
            _lastAppliedCursor = targetCursor;
        }
        
        _primaryMouse.Cursor.StandardCursor = targetCursor;
        _lastAppliedCursor = targetCursor;
        
        if (_pendingAction != null)
        {
            var action = _pendingAction;
            _pendingAction = null; 
            action();
        }
    }
    private void OnResize(Silk.NET.Maths.Vector2D<int> size) => SetupSurface();

    private void SetupSurface()
    {
        var target = new GRBackendRenderTarget(_window.Size.X, _window.Size.Y, 0, 8, new GRGlFramebufferInfo(0, 0x8058));
        _surface = SKSurface.Create(_grContext, target, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
    }
    
    public void Dispose()
    {
        _surface?.Dispose();
        _grContext?.Dispose();
        _window?.Dispose();
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
                onYes: () => {
                    _inputHandler.HandleShortcut(true, false, "S"); 
                    FinishClosingTab();
                },
                onNo: () => {
                    FinishClosingTab();
                },
                onCancel: () => { 
                }
            );
        }
        else
        {
            FinishClosingTab();
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
    }

    public void Run() => _window.Run();
}