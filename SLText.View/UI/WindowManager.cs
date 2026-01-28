using Silk.NET.Input;
using Silk.NET.Windowing;
using SkiaSharp;
using SLText.Core.Engine;
using SLText.View.Components;
using System.Runtime.InteropServices;
using NativeFileDialogSharp;
using Silk.NET.Core;
using SLText.View.Styles;
using SLText.View.UI.Input;
using TextCopy;

namespace SLText.View.UI;

public class WindowManager
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
    
    private readonly SyntaxProvider _syntaxProvider = new(); 
    private List<(string pattern, SKColor color)> _currentRules = new();

    public WindowManager(TextBuffer buffer, CursorManager cursor, InputHandler input, string? initialFilePath = null)
    {
        var options = WindowOptions.Default;
        options.Size = new Silk.NET.Maths.Vector2D<int>(800, 600);
        options.Title = "SLText";
        _window = Window.Create(options);
        _editor = new EditorComponent(buffer, cursor);
        _inputHandler = input;
        
        _buffer = buffer;
        _cursor = cursor;
        
        _editor = new EditorComponent(buffer, cursor);
        _statusBar = new StatusBarComponent(cursor, buffer);
        _inputHandler = input;
        
        _currentFilePath = initialFilePath;
        
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Update += OnUpdate;
        _window.FramebufferResize += OnResize;
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
                _inputHandler.HandleTextInput(c);
                if (!_isDirty) { _isDirty = true; UpdateTitle(); }
            };
        }
        
        foreach (var mouse in input.Mice)
        {
            mouse.Scroll += OnMouseScroll;
        }

        var handle = _window.Native!.Win32!.Value.Hwnd;
        int useDarkMode = 1;
        DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
        
        if (!string.IsNullOrEmpty(_currentFilePath) && File.Exists(_currentFilePath))
        {
            try 
            {
                string content = File.ReadAllText(_currentFilePath);
                _buffer.LoadText(content);
                _statusBar.LanguageName = _editor.UpdateSyntax(_currentFilePath);
                UpdateTitle(); 
            }
            catch (Exception ex)
            {
                _currentFilePath = null;
            }
        }
        else 
        {
            UpdateTitle(); 
        }
        
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel scroll)
    {
        _editor.ScrollY -= scroll.Y * 60;
        if (_editor.ScrollY < 0) _editor.ScrollY = 0;
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
        bool ctrl = k.IsKeyPressed(Key.ControlLeft) || k.IsKeyPressed(Key.ControlRight);
        bool shift = k.IsKeyPressed(Key.ShiftLeft) || k.IsKeyPressed(Key.ShiftRight);
        
        bool cursorMoved = false;
        
        if (ctrl && key == Key.O)
        {
            var result = Dialog.FileOpen("txt,cs,html,htm,css,js,razor,cshtml,xml,csproj,gcode,nc,cnc,tap");
            if (result.IsOk)
            {
                _currentFilePath = result.Path;

                string content = File.ReadAllText(_currentFilePath).Replace("\t", "    ");
                _buffer.LoadText(content);
    
                string langName = _editor.UpdateSyntax(_currentFilePath);
                _statusBar.LanguageName = langName;
    
                _cursor.SetPosition(0, 0); 
                _editor.ScrollY = 0;
                _isDirty = false;
                UpdateTitle();
            }
            return;
        }
        
        if (ctrl && key == Key.S)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                var result = Dialog.FileSave("txt,cs,html,htm,css,js,razor,cshtml,xml,csproj,gcode,nc,cnc,tap");
                if (result.IsOk)
                {
                    _currentFilePath = result.Path;
                }
                else return;
            }

            File.WriteAllText(_currentFilePath, _buffer.GetAllText());
            _isDirty = false;
            UpdateTitle();
            return;
        }
        
        if (ctrl && key == Key.V)
        {
            string? text = ClipboardService.GetText();
            if (!string.IsNullOrEmpty(text))
            {
                _inputHandler.HandlePaste(text);
                cursorMoved = true;
            }
            return;
        }
        
        if (ctrl || shift)
        {
            string shortcutKey = KeyboardMapper.MapShortcutKey(key);
            _inputHandler.HandleShortcut(ctrl, shift, shortcutKey);
            cursorMoved = true;
            
        } else
        {
            string? navKey = KeyboardMapper.MapNavigationKey(key);
            if (navKey != null)
            {
                _inputHandler.HandleShortcut(false, false, navKey);
                cursorMoved = true;

                if (key == Key.Enter || key == Key.Backspace || key == Key.Delete)
                {
                    if (!_isDirty) { _isDirty = true; UpdateTitle(); }
                }
            }
        }

        if (cursorMoved)
        {
            _editor.RequestScrollToCursor();
        }
    }

    private void OnRender(double dt)
    {
        var canvas = _surface.Canvas;
        canvas.Clear(new SKColor(30, 30, 30));

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

        _grContext.Flush();
    }

    private void OnUpdate(double dt) => _editor.Update(dt);
    private void OnResize(Silk.NET.Maths.Vector2D<int> size) => SetupSurface();

    private void SetupSurface()
    {
        var target = new GRBackendRenderTarget(_window.Size.X, _window.Size.Y, 0, 8, new GRGlFramebufferInfo(0, 0x8058));
        _surface = SKSurface.Create(_grContext, target, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
    }

    public void Run() => _window.Run();
}