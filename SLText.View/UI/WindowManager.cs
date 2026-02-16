using Silk.NET.Input;
using Silk.NET.Windowing;
using SkiaSharp;
using SLText.Core.Engine;
using SLText.View.Components;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using SLText.Core.Engine.LSP;
using SLText.Core.Engine.Model;
using SLText.View.Services;
using SLText.View.Styles;
using SLText.View.UI.Input;

namespace SLText.View.UI;

public partial class WindowManager : IDisposable
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

    private Key? _lastPressedKey;
    private double _repeatTimer = 0;
    private double _initialDelay = 0.5;
    private double _repeatInterval = 0.03;
    private bool _isFirstRepeat = true;
    private IKeyboard? _activeKeyboard;


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
    
    private LspService _lspService = new();
    private AutocompleteComponent _autocomplete;
    private SignatureHelpComponent _signatureHelp;
    private CancellationTokenSource? _diagnosticCts;

    private RunService _runService = new();
    private RunConfiguration? _activeConfiguration;
    private string _lastDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private EditorSettings _settings;
    private bool _isLoadingSession = true;

    public WindowManager(TextBuffer buffer, CursorManager cursor, InputHandler input, string? initialFilePath, EditorSettings settings)
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
        _tabManager.AddTab(buffer, cursor, null);
        _editor.SetCurrentData(_tabManager.ActiveTab!.Buffer, _tabManager.ActiveTab.Cursor);        _terminal = new TerminalComponent();
        _autocomplete = new AutocompleteComponent(_editor.Font);
        _signatureHelp = new SignatureHelpComponent();
        
        _settings = settings;
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

        _inputHandler.OnNextTabRequested += () =>
        {
            _tabManager.NextTab();
            SyncActiveTab(false);
        };

        _inputHandler.OnPreviousTabRequested += () =>
        {
            _tabManager.PreviousTab();
            SyncActiveTab(false);
        };

        _inputHandler.OnToggleExplorerRequested += () =>
        {
            _explorer.IsVisible = !_explorer.IsVisible;
        };

        _inputHandler.OnOpenFolderRequested += () =>
        {
            if (_inputHandler.GetDialogService() is NativeDialogService dialogs)
            {
                string? folder = dialogs.OpenFolder(_inputHandler.GetLastDirectory());

                if (!string.IsNullOrEmpty(folder))
                {
                    _lastDirectory = folder;
                    var settings = SettingsService.Load();
                    settings.LastRootDirectory = folder;
                    SettingsService.SaveImmediate(settings);
                    
                    _explorer.SetRootDirectory(folder);
                    _explorer.IsVisible = true;
                    _terminal.SetWorkingDirectory(folder);
                    _runService.ScanProject(folder);
                    _editor.SetDiagnostics(new List<LspService.MappedDiagnostic>());
                    _terminal.ShowDiagnostics(new List<LspService.MappedDiagnostic>(), "Project Loaded");
                    _diagnosticCts?.Cancel();
                    
                    _terminal.WriteOutput("Output", $"Open folder: {folder}", clearFirst: true);
                    
                    Task.Run(() => 
                    {
                        try 
                        {
                            _lspService.LoadProjectFiles(folder, (statusMessage) => 
                            {
                                _terminal.WriteOutput("Output", statusMessage);
                            });

                            RequestDiagnostics(instant: true);
                        }
                        catch (Exception ex)
                        {
                            _terminal.WriteOutput("Output", $"[FATAL ERROR] Failed to load project: {ex.Message}");
                        }
                    });
                }
            }
        };

        _inputHandler.OnFocusExplorerSearchRequested += () =>
        {
            _explorer.IsVisible = true;
            _explorer.IsFocused = true;
        };

        _inputHandler.OnThemeToggleRequested += () =>
        {
            if (_currentTheme.Background.Red < 128)
            {
                ApplyTheme(EditorTheme.Light);
                SyncSettings();
            }
            else
            {
                ApplyTheme(EditorTheme.Dark);
                SyncSettings();
            }
                
        };

        _inputHandler.OnNewTerminalTabRequested += () =>
        {
            if (!_terminal.IsVisible) _terminal.IsVisible = true;

            _isTerminalFocused = true;
            _terminal.CreateNewTab("bash");
        };

        _inputHandler.OnTerminalInterruptRequested += () =>
        {
            if (_isTerminalFocused && _terminal.IsVisible)
            {
                _terminal.InterruptActiveTerminal();
            }
            else if (!_isTerminalFocused)
            {
                _inputHandler.HandleCopy();
            }
        };

        _inputHandler.OnRunRequested += () =>
        {
            ExecuteActiveConfiguration();
        };

        _inputHandler.OnRunConfigurationSelectorRequested += () =>
        {
            OpenRunConfigurationSelector();
        };

        _inputHandler.OnStopRequested += () =>
        {
            _terminal.ShutdownAllTerminals();
        };

        _editor.OnRunTestRequested += (line) =>
        {
            HandleRunTest(line);
        };
        
        _editor.OnQuickFixRequested += async (line, symbolText) =>
        {
            string ext = Path.GetExtension(_currentFilePath ?? "").ToLower();
            if (ext != ".cs") return;
            
            var namespaces = await _lspService.GetTypeNamespacesAsync(symbolText);
    
            if (namespaces.Any())
            {
                var suggestions = namespaces.Select(ns => $"using {ns};").ToList();
        
                var pos = _editor.GetCursorScreenPosition(); 
        
                _cursor.SetPosition(line, 0);
                pos = _editor.GetCursorScreenPosition();

                _autocomplete.Show(pos.x + 30, pos.y, suggestions);
        
            }
        };
        
        _inputHandler.OnReloadProjectRequested += () =>
        {
            if (string.IsNullOrEmpty(_lastDirectory))
            {
                _modal.Show("Aviso", "Nenhum diretório aberto para recarregar.", null, null, null);
                return;
            }

            if (!_terminal.IsVisible) _terminal.IsVisible = true;
    
            var buildTab = _terminal.CreateNewTab("Build-Reload", _lastDirectory, forceNew: false);
    
            lock (buildTab.OutputLines) { buildTab.OutputLines.Clear(); buildTab.OutputLines.Add("--- Starting Build for Reload ---"); }
    
            buildTab.Service.SendCommand("dotnet build\n");
            _terminal.ShowDiagnostics(new List<LspService.MappedDiagnostic>(), "Reloading References...");

            Task.Run(async () =>
            {
                try 
                {
                    await Task.Delay(5000);

                    _terminal.WriteOutput("Output", "Reloading references after build...", clearFirst: false);

                    _lspService.LoadProjectFiles(_lastDirectory, (msg) => 
                    {
                        _terminal.WriteOutput("Output", msg);
                    });
                    
                    _editor.SetDiagnostics(new List<LspService.MappedDiagnostic>());
                    RequestDiagnostics(instant: true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reloading project: {ex.Message}");
                }
            });
        };
        
        _explorer.OnFileOpenRequested += (path) => 
        {
            SetCurrentFile(path);
            _explorer.IsFocused = false; 
        };
    }
    

    private void HandleRunTest(int lineNumber)
    {
        string codeLine = _buffer.GetLine(lineNumber + 1);

        var match = Regex.Match(codeLine, @"(class|void|Task)\s+([\w\d_]+)");
        if (!match.Success) return;

        string identifier = match.Groups[2].Value;

        var testConfig = new RunConfiguration
        {
            Name = $"Test: {identifier}",
            Command = $"dotnet test --filter FullyQualifiedName~{identifier}",
            WorkingDirectory = _lastDirectory
        };

        RunSingleConfig(testConfig);
    }

    public void OpenSearch()
    {
        _search.IsVisible = true;
        _search.Clear();
    }
    
    private string GetPartialWord(string line, int column)
    {
        if (string.IsNullOrEmpty(line) || column == 0) return "";

        int start = column - 1;
        while (start >= 0)
        {
            char c = line[start];
            if (!char.IsLetterOrDigit(c) && c != '_') 
            {
                break; 
            }
            start--;
        }
    
        return line.Substring(start + 1, column - (start + 1));
    }

    private void OpenRunConfigurationSelector()
    {
        _commandPalette.IsVisible = true;

        var runCommands = _runService.Configurations.Select(config => new EditorCommand(
            config.Name,
            "Run Configuration",
            () =>
            {
                _activeConfiguration = config;
                _statusBar.SetActiveConfiguration(config);
                _commandPalette.IsVisible = false;
                ExecuteActiveConfiguration();
            }
        )).ToList();

        _commandPalette.LoadCommands(runCommands);
    }

    private void ExecuteActiveConfiguration()
    {
        var config = _activeConfiguration;
        if (config == null) return;

        if (config.Type == RunType.Compound)
        {
            foreach (var childId in config.ChildrenIds)
            {
                var child = _runService.Configurations.FirstOrDefault(c => c.Id == childId);
                if (child != null) RunSingleConfig(child);
            }
        }
        else
        {
            RunSingleConfig(config);
        }
    }

    private async void RunSingleConfig(RunConfiguration config)
    {
        _terminal.IsVisible = true;
        var tab = _terminal.CreateNewTab(config.Name, config.WorkingDirectory);
        await Task.Delay(600);
        tab.Service.SendCommand(config.Command + "\n");
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

    private bool IsNavigationOnly(Key key) =>
        key is Key.Up or Key.Down or Key.Left or Key.Right or Key.PageUp or Key.PageDown or Key.Home or Key.End;

    public void OnSaveSuccess(string? path)
    {
        var active = _tabManager.ActiveTab;
        if (active == null || path == null) return;

        active.FilePath = path;
        active.IsDirty = false;
        _currentFilePath = path;

        _statusBar.LanguageName = _editor.UpdateSyntax(path);
        UpdateTitle();
        SyncSettings();
    }

    private StandardCursor _lastAppliedCursor = StandardCursor.Default;
    
    private void OnResize(Silk.NET.Maths.Vector2D<int> size) => SetupSurface();

    private void SetupSurface()
    {
        if (_surface != null)
        {
            _surface.Dispose();
            _surface = null;
        }

        if (_grContext == null) return;

        var width = Math.Max(1, _window.Size.X);
        var height = Math.Max(1, _window.Size.Y);
        
        var target = new GRBackendRenderTarget(width, height, 0, 8, new GRGlFramebufferInfo(0, 0x8058)); // 0x8058 = GL_RGBA8
    
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