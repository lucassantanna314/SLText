using System.Runtime.InteropServices;
using Silk.NET.Input;
using SkiaSharp;
using SLText.Core.Engine;
using SLText.View.Services;
using SLText.View.Styles;
using SLText.View.UI.Input;

namespace SLText.View.UI;

public partial class WindowManager
{
    private void OnLoad()
    {
        _isLoadingSession = true;
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
            _activeKeyboard = keyboard;
            keyboard.KeyDown += OnKeyDown;
            
            keyboard.KeyUp += (k, key, scancode) => {
                if (key == _lastPressedKey) _lastPressedKey = null;
            };
            
            
            keyboard.KeyChar += async (k, c) =>
            {
                if (_commandPalette.IsVisible)
                {
                    _commandPalette.HandleInput(c.ToString(), false);
                    return;
                }

                if (_explorer.IsFocused)
                {
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
                
                RequestDiagnostics();
                
                try 
                {
                    string ext = Path.GetExtension(_currentFilePath ?? "").ToLower();
                    if (ext != ".cs" && ext != ".razor") return;
                    
                    if (char.IsLetterOrDigit(c) || c == '.' || c == '_')
                    {
                        var pos = _editor.GetCursorScreenPosition();
                        int flatOffset = _buffer.GetFlatOffset(_cursor.Line, _cursor.Column);
                        string allText = _buffer.GetAllText();

                        string partialWord = GetPartialWord(_buffer.GetLine(_cursor.Line), _cursor.Column);

                        // 2. Chama o LSP
                        string path = _currentFilePath ?? "new_file.cs";
                        var completions = await _lspService.GetCompletionsAsync(allText, flatOffset, path);

                        if (completions != null && completions.Any())
                        {
                            var filtered = completions
                                .Select(i => i.DisplayText)
                                .Where(text => text.StartsWith(partialWord, StringComparison.OrdinalIgnoreCase))
                                .ToList();

                            if (filtered.Any())
                            {
                                _autocomplete.Show(pos.x, pos.y + 20, filtered); 
                            }
                            else
                            {
                                _autocomplete.IsVisible = false;
                            }
                        }
                        else
                        {
                            _autocomplete.IsVisible = false;
                        }
                    }
                    else
                    {
                        _autocomplete.IsVisible = false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"UI Completion Dispatch Error: {ex.Message}");
                }
                
                //help
                if (c == '(' || c == ',')
                {
                    string ext = Path.GetExtension(_currentFilePath ?? "").ToLower();
                    if (ext != ".cs" && ext != ".razor") return;
                    
                    var pos = _editor.GetCursorScreenPosition();
                    int flatOffset = _buffer.GetFlatOffset(_cursor.Line, _cursor.Column);
                    string code = _buffer.GetAllText();
                    string path = _currentFilePath ?? "new_file.cs";

                    var sigData = await _lspService.GetSignatureHelpAsync(code, flatOffset, path);
        
                    if (sigData != null && sigData.Signatures.Any())
                    {
                        _signatureHelp.Show(pos.x, pos.y, sigData);
                    }
                }
                else if (c == ')')
                {
                    _signatureHelp.IsVisible = false; 
                }
                
                else if (_signatureHelp.IsVisible)
                {
                    var pos = _editor.GetCursorScreenPosition();
                    int flatOffset = _buffer.GetFlatOffset(_cursor.Line, _cursor.Column);
                    var sigData = await _lspService.GetSignatureHelpAsync(_buffer.GetAllText(), flatOffset, _currentFilePath ?? "new.cs");
                    if (sigData != null) _signatureHelp.Show(pos.x, pos.y, sigData);
                    else _signatureHelp.IsVisible = false;
                }
            };
        }

        _mouseHandler = new MouseHandler(_editor, _cursor, _inputHandler, input, _modal);

        foreach (var mouse in input.Mice)
        {
            mouse.MouseDown += (m, button) =>
            {
                var pos = m.Position;
                
                if (_autocomplete.IsVisible && _autocomplete.Bounds.Contains(pos.X, pos.Y))
                {
                    bool clickedValidItem = _autocomplete.SelectIndexByMouseY(pos.Y); 

                    if (clickedValidItem)
                    {
                        ApplyAutocomplete(); 
                        return; 
                    }
                }
                
                if (_autocomplete.IsVisible) _autocomplete.IsVisible = false;
                if (_signatureHelp.IsVisible) _signatureHelp.IsVisible = false;
                
                float explorerWidth = _explorer.IsVisible ? _explorer.Width : 0;


                if (_statusBar.SelectorBounds.Contains(pos.X, pos.Y))
                {
                    OpenRunConfigurationSelector();
                    return;
                }

                if (_statusBar.PlayButtonBounds.Contains(pos.X, pos.Y))
                {
                    ExecuteActiveConfiguration();
                    return;
                }

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
                            _explorer.HandleMouseClick(node); 
                
                            if (!node.IsDirectory)
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
            if (_autocomplete.IsVisible) _autocomplete.IsVisible = false;
            if (_signatureHelp.IsVisible) _signatureHelp.IsVisible = false;
            _editor.ApplyScroll(deltaX, deltaY);
        };

        _inputHandler.OnZoomRequested += (delta) =>
        {
            if (_autocomplete.IsVisible) _autocomplete.IsVisible = false;
            if (_signatureHelp.IsVisible) _signatureHelp.IsVisible = false;
            _editor.FontSize += delta;
            var settings = SettingsService.Load();
            settings.FontSize = _editor.FontSize;
            SettingsService.SaveDebounced(settings);
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
        
        if (_settings.Theme == "Light") ApplyTheme(EditorTheme.Light);
        else ApplyTheme(EditorTheme.Dark);
        ApplySavedFontSize(_settings.FontSize);
        
        if (!string.IsNullOrEmpty(_settings.LastRootDirectory) && Directory.Exists(_settings.LastRootDirectory))
        {
            SetCurrentFile(_settings.LastRootDirectory);
        }

        if (_settings.OpenTabs != null && _settings.OpenTabs.Count > 0)
        {
            _tabManager.Tabs.Clear(); 
            
            foreach (var filePath in _settings.OpenTabs)
            {
                if (File.Exists(filePath))
                {
                    try 
                    {
                        var content = File.ReadAllText(filePath).Replace("\t", "    ");
                
                        var buf = new TextBuffer(); 
                        buf.LoadText(content); 
                
                        var cur = new CursorManager(buf);
                        _tabManager.AddTab(buf, cur, filePath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao restaurar aba {filePath}: {ex.Message}");
                    }
                }
            }
    
            if (_tabManager.Tabs.Count > 0)
            {
                _cursor.SetPosition(0, 0);
                _tabManager.SelectTab(0);
            }
        }
        
        if (!string.IsNullOrEmpty(_currentFilePath) && File.Exists(_currentFilePath))
        {
            SetCurrentFile(_currentFilePath);
        }
        
        if (_tabManager.Tabs.Count == 0)
        {
            _tabManager.AddTab(_buffer, _cursor, null);
        }
        
        _isLoadingSession = false;
        _cursor.SetPosition(0, 0);
        SyncActiveTab(true);
        _editor.SetScroll(0, 0);
        
        UpdateTitle();
    }
}