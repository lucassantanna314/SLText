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
        //StartupLog.Write("OnLoad: begin");

        _isLoadingSession = true;

        // Inicializa Skia com o contexto da GPU da Silk.NET.
        // Falha aqui precisa ser explícita: sem este check o primeiro frame morre com um
        // NullReferenceException em OnRender, sem indicar que o problema foi o contexto GL.
        //
        // O resolver precisa vir do GLFW: GRGlInterface.Create() sem argumentos usa o loader nativo
        // do Skia, que só reconhece um contexto GLX corrente e retorna null em sessão Wayland, onde
        // o GLFW 3.4 cria contexto EGL. glfwGetProcAddress devolve o ponteiro certo nos dois casos.
        //StartupLog.Write("OnLoad: creating GRGlInterface");
        var glContext = _window.GLContext
            ?? throw new InvalidOperationException(
                "A janela não expõe um contexto OpenGL (GLContext null). A GraphicsAPI precisa ser OpenGL.");
        var glInterface = GRGlInterface.Create(proc => glContext.GetProcAddress(proc));
       // StartupLog.Write("OnLoad: GRGlInterface.Create returned", glInterface == null ? "NULL" : "ok");
        if (glInterface == null)
        {
            throw new InvalidOperationException(
                "Não foi possível criar a interface OpenGL (GRGlInterface.Create retornou null). " +
                "Verifique se há um driver OpenGL disponível e se a sessão é X11 ou Wayland com suporte a GL.");
        }

        _grContext = GRContext.CreateGl(glInterface);
        //StartupLog.Write("OnLoad: GRContext.CreateGl returned", _grContext == null ? "NULL" : "ok");
        if (_grContext == null)
        {
            throw new InvalidOperationException(
                "Não foi possível criar o contexto GL do SkiaSharp (GRContext.CreateGl retornou null). " +
                "Verifique o driver de vídeo / mesa e, em Wayland, tente iniciar uma sessão X11.");
        }

        SetupSurface();
        //StartupLog.Write("OnLoad: surface ready", _surface == null ? "SURFACE IS NULL" : "ok");
        SetWindowIcon();
        //StartupLog.Write("OnLoad: window icon set");

        // Configura Input da Silk.NET
        var input = _window.CreateInput();
       // StartupLog.Write("OnLoad: input created",
        //    $"keyboards={input.Keyboards.Count} mice={input.Mice.Count}");
        _primaryMouse = input.Mice[0];

        foreach (var keyboard in input.Keyboards)
        {
            _activeKeyboard = keyboard;
            keyboard.KeyDown += OnKeyDown;

            keyboard.KeyUp += (k, key, scancode) =>
            {
                if (key == _lastPressedKey) _lastPressedKey = null;
            };


            keyboard.KeyChar += (k, c) =>
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

                if (!IsAnalyzableFile(_currentFilePath)) return;

                if (char.IsLetterOrDigit(c) || c == '.' || c == '_') RequestCompletions();
                else _autocomplete.IsVisible = false;

                if (c == '(' || c == ',') RequestSignatureHelp();
                else if (c == ')') _signatureHelp.IsVisible = false;
                else if (_signatureHelp.IsVisible) RequestSignatureHelp();
            };
        }

        _mouseHandler = new MouseHandler(_editor, _cursor, _inputHandler, input, _modal);

        foreach (var mouse in input.Mice)
        {
            mouse.MouseDown += (m, button) =>
            {
                var pos = m.Position;

                // --- Centralized input dispatch ---
                if (_inputManager.ProcessClick(pos.X, pos.Y)) return;

                // Default behavior when no overlay consumed the click:
                // Right-click opens context menu
                if (button == MouseButton.Right && _editor.Bounds.Contains(pos.X, pos.Y))
                {
                    _editor.HandleRightClick(pos.X, pos.Y);
                    return;
                }
            };

            mouse.MouseMove += (m, pos) =>
            {
                if (_contextMenu.IsVisible)
                {
                    _contextMenu.OnMouseMove(pos.X, pos.Y);
                }

                if (_isResizingExplorer)
                {
                    _explorer.Width = Math.Clamp(pos.X, 100, 500);
                    return;
                }

                // Cursor shape is owned by OnUpdate, which already handles the terminal and
                // explorer splitters and only touches GLFW when the shape actually changes.
                if (_terminal.IsResizing)
                {
                    _terminal.OnMouseMove(pos.X, pos.Y, _window.Size.Y);
                    return;
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

                // --- Centralized input dispatch ---
                if (_inputManager.ProcessWheel(pos.X, pos.Y, scroll.Y * 10)) return;

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

        var savedTheme = AvailableThemes.FirstOrDefault(t => t.Name == _settings.Theme) ?? EditorTheme.Dark;
        ApplyTheme(savedTheme);
        _currentThemeIndex = AvailableThemes.IndexOf(savedTheme);
        if (_currentThemeIndex == -1) _currentThemeIndex = 0;

        ApplySavedFontSize(_settings.FontSize);

        if (!string.IsNullOrEmpty(_settings.LastRootDirectory) && Directory.Exists(_settings.LastRootDirectory))
        {
            _lastDirectory = _settings.LastRootDirectory; // Ensure lastDirectory is set for git auto-connect
            SetCurrentFile(_settings.LastRootDirectory);

            // --- Auto-connect to git on startup (Fase 2) ---
            Task.Run(async () => await ConnectToRepository(_lastDirectory));
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
                        var content = File.ReadAllText(filePath);

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
        //StartupLog.Write("OnLoad: complete - event loop should now render frames");
    }

}
