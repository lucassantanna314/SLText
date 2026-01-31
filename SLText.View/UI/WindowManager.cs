using Silk.NET.Input;
using Silk.NET.Windowing;
using SkiaSharp;
using SLText.Core.Engine;
using SLText.View.Components;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using SLText.View.Components.Canvas;
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

    private void ApplyTheme(EditorTheme theme)
    {
        _currentTheme = theme;
        _editor.SetTheme(theme);
        _statusBar.ApplyTheme(theme);
    }

    public WindowManager(TextBuffer buffer, CursorManager cursor, InputHandler input, string? initialFilePath = null)
    {
        var options = WindowOptions.Default;
        options.Size = new Silk.NET.Maths.Vector2D<int>(800, 600);
        options.Title = "SLText";
        _window = Window.Create(options);
        
        
        _buffer = buffer;
        _cursor = cursor;
        _inputHandler = input;
        
        // 2. componentes visuais
        _editor = new EditorComponent(buffer, cursor);
        _inputHandler.AddEditorShortcuts(_editor);
        
        _statusBar = new StatusBarComponent(cursor, buffer, _editor);
        
        _currentFilePath = initialFilePath;
        
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Update += OnUpdate;
        _window.FramebufferResize += OnResize;
        
        if (input.GetDialogService() is NativeDialogService nativeDialog)
        {
            nativeDialog.Modal = _modal;
        }
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
        
        foreach (var keyboard in input.Keyboards)
        {
            keyboard.KeyDown += OnKeyDown;
            
            keyboard.KeyChar += (k, c) => 
            {
                if (_modal.IsVisible || _modal.IsRecentlyClosed) return;
                
                bool ctrl = k.IsKeyPressed(Key.ControlLeft) || k.IsKeyPressed(Key.ControlRight);
                if (ctrl) return;
                
                _inputHandler.HandleTextInput(c);
                if (!_isDirty) { _isDirty = true; UpdateTitle(); }
            };
        }
        
        _mouseHandler = new MouseHandler(_editor, _cursor, _inputHandler, input, _modal);
        
        foreach (var mouse in input.Mice)
        {
            mouse.MouseDown += _mouseHandler.OnMouseDown;
            mouse.MouseMove += _mouseHandler.OnMouseMove; 
            mouse.MouseUp += _mouseHandler.OnMouseUp;     
            mouse.Scroll += _mouseHandler.OnMouseScroll;
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
        string fileName = string.IsNullOrEmpty(_currentFilePath) 
            ? "New File" 
            : Path.GetFileName(_currentFilePath);
    
        string dirtyFlag = _isDirty ? "*" : "";
    
        _window.Title = $"SLText - {fileName}{dirtyFlag}";
    }

    private void OnKeyDown(IKeyboard k, Key key, int arg3)
    {
        string? mappedKey = KeyboardMapper.Normalize(key);
        if (mappedKey == null) return;

        if (_modal.IsVisible)
        {
            if (_modal.HandleKeyDown(mappedKey)) return;
        }
        
        bool ctrl = k.IsKeyPressed(Key.ControlLeft) || k.IsKeyPressed(Key.ControlRight);
        bool shift = k.IsKeyPressed(Key.ShiftLeft) || k.IsKeyPressed(Key.ShiftRight);

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
        float headerHeight = 0;
        float footerHeight = 25;
        
        // Zona do Editor 
        _editor.Bounds = new SKRect(0, headerHeight, width, height - footerHeight);
        _editor.Render(canvas);
        
        // Define e Renderiza a StatusBar (Rodapé)
        _statusBar.Bounds = new SKRect(0, height - footerHeight, width, height);
        
        _statusBar.FileInfo = string.IsNullOrEmpty(_currentFilePath) ? "New File" : Path.GetFileName(_currentFilePath);
        _statusBar.Render(canvas);
        
        if (_modal.IsVisible)
        {
            _modal.Render(canvas, new SKRect(0, 0, width, height), _currentTheme);
        }

        _grContext.Flush();
    }
    
    public void SetCurrentFile(string path, bool resetCursor = false)
    {
        _currentFilePath = path;
        _isDirty = false;
        
        _inputHandler.UpdateLastDirectory(path);
        
        if (resetCursor)
        {
            _cursor.SetPosition(0, 0); 
            _editor.ScrollY = 0;
        }

        string langName = _editor.UpdateSyntax(path);
        _statusBar.LanguageName = langName;
        _inputHandler.UpdateCurrentPath(path);
    
        UpdateTitle();
    }

    private void OnUpdate(double dt)
    {
        _editor.Update(dt);

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

    public void Run() => _window.Run();
}