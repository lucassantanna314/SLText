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
using SLText.Components;

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
    private Action? _pendingAction;

    /// <summary>Tab the renderer is currently pointed at; avoids re-binding every frame.</summary>
    private TabInfo? _renderedTab;

    /// <summary>Startup tracing only: logs the first successfully rendered frame.</summary>
    private bool _firstFrameLogged;

    private Key? _lastPressedKey;
    private double _repeatTimer = 0;
    private double _initialDelay = 0.5;
    private double _repeatInterval = 0.03;
    private bool _isFirstRepeat = true;
    private IKeyboard? _activeKeyboard;


    private EditorTheme _currentTheme = EditorTheme.Dark;

    private MouseHandler? _mouseHandler;
    private ModalComponent _modal = new();
    private SearchComponent _search = new();
    private ContextMenuComponent _contextMenu = new();

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
    private int _currentThemeIndex = 0;

    // --- GitHub Integration (Fase 2-3) ---
    private readonly Core.Engine.Git.GitHubIntegrationService _gitHubService = new();
    private BranchSelectorOverlay _branchSelector = new();

    public WindowManager(TextBuffer buffer, CursorManager cursor, InputHandler input, string? initialFilePath, EditorSettings settings)
    {
        var options = WindowOptions.Default;
        options.Size = new Silk.NET.Maths.Vector2D<int>(800, 600);
        options.Title = "SLText";
        // StartupLog.Write("WindowManager ctor: creating GLFW window");
        _window = Window.Create(options);
        //StartupLog.Write("WindowManager ctor: window created",
        //    $"API={_window.API} size={_window.Size.X}x{_window.Size.Y}");
        _window.Closing += OnWindowClosing;


        _buffer = buffer;
        _cursor = cursor;
        _inputHandler = input;

        // 2. componentes visuais
        _editor = new EditorComponent(buffer, cursor);
        _inputHandler.AddEditorShortcuts(_editor);

        _statusBar = new StatusBarComponent(cursor, buffer, _editor);

        // Wire GitHub integration events (Fase 2) — MUST be after _statusBar created
        _gitHubService.ConnectionStateChanged += (_, state) =>
        {
            InvokeOnUi(() =>
            {
                if (_statusBar == null) return;
                
                if (state.HasActiveRepo && !string.IsNullOrEmpty(state.CurrentBranch))
                {
                    _statusBar.SetBranchName(state.CurrentBranch);
                    _statusBar.SetGitConnection(true);
                    RefreshExplorerWithGit();
                }
                else
                {
                    _statusBar.SetBranchName(null);
                    _statusBar.SetGitConnection(false);
                    _explorer.ClearSearch(); // No-op if not focused
                }
            });
        };

        _tabManager = new TabManager();
        _tabComponent = new TabComponent(_tabManager);
        _tabManager.AddTab(buffer, cursor, null);
        _editor.SetCurrentData(_tabManager.ActiveTab!.Buffer, _tabManager.ActiveTab.Cursor); _terminal = new TerminalComponent();
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

        _inputHandler.OnNextTabRequested += () => SwitchTab(+1);

        _inputHandler.OnPreviousTabRequested += () => SwitchTab(-1);

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

                    Task.Run(async () =>
                    {
                        try
                        {
                            _lspService.LoadProjectFiles(folder, (statusMessage) =>
                            {
                                _terminal.WriteOutput("Output", statusMessage);
                            });

                            RequestDiagnostics(instant: true);

                            // --- Auto-connect to git if repo exists (Fase 2) ---
                            await ConnectToRepository(folder);
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

         _currentThemeIndex = AvailableThemes.IndexOf(_currentTheme);
        if (_currentThemeIndex == -1) _currentThemeIndex = 0;

        _inputHandler.OnThemeToggleRequested += () =>
        {
            _currentThemeIndex = (_currentThemeIndex + 1) % AvailableThemes.Count;
            ApplyTheme(AvailableThemes[_currentThemeIndex]);
            SyncSettings();
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

        _inputHandler.OnRunRequested += ExecuteActiveConfiguration;

        _inputHandler.OnRunConfigurationSelectorRequested += OpenRunConfigurationSelector;

        _inputHandler.OnStopRequested += () =>
        {
            _terminal.ShutdownAllTerminals();
        };

        _editor.OnRunTestRequested += HandleRunTest;

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

        _editor.OnRightClickRequested += (screenX, screenY, line, col) =>
        {
            var items = new List<ContextMenuItem>();

            string ext = Path.GetExtension(_currentFilePath ?? "").ToLowerInvariant();
            if (ext is ".cs" or ".razor")
            {
                string currentLine = _buffer.GetLine(Math.Clamp(line, 0, _buffer.LineCount - 1));
                int bufferCol = Math.Clamp(col, 0, currentLine.Length);

                // .razor: pode ter @ antes do identificador — verifique se há identificador válido a seguir
                if (ext == ".razor" && bufferCol >= 0 && currentLine[bufferCol] == '@' && currentLine.Length > bufferCol + 1)
                {
                    char nextChar = currentLine[bufferCol + 1];
                    if (char.IsLetterOrDigit(nextChar))
                    {
                        bufferCol++;
                    }
                    else
                    {
                        bufferCol = -1;
                    }
                }

                bool hasIdentifier = bufferCol >= 0 && bufferCol < currentLine.Length
                    && (char.IsLetterOrDigit(currentLine[bufferCol]) || currentLine[bufferCol] == '_');

                // Pega a palavra inteira para checar contra keywords C#
                string word = "";
                if (hasIdentifier)
                {
                    int start = bufferCol;
                    while (start > 0 && (char.IsLetterOrDigit(currentLine[start - 1]) || currentLine[start - 1] == '_'))
                        start--;
                    int end = bufferCol;
                    while (end < currentLine.Length && (char.IsLetterOrDigit(currentLine[end]) || currentLine[end] == '_'))
                        end++;
                    word = currentLine.Substring(start, end - start);
                    bool isKeyword = word switch
                    {
                        "abstract" or "as" or "base" or "break" or "case" or "catch" or "class" or "const"
                        or "continue" or "decimal" or "default" or "do" or "else" or "enum" or "event"
                        or "explicit" or "extern" or "false" or "finally" or "for" or "foreach" or "goto"
                        or "if" or "implicit" or "in" or "interface" or "internal" or "is" or "lock"
                        or "long" or "namespace" or "new" or "null" or "object" or "operator" or "out"
                        or "override" or "params" or "private" or "protected" or "public" or "readonly"
                        or "return" or "sealed" or "sizeof" or "static" or "struct" or "switch" or "throw"
                        or "true" or "try" or "typeof" or "uint" or "ulong" or "unchecked" or "unsafe"
                        or "ushort" or "using" or "virtual" or "void" or "while" => true,
                        _ => false
                    };
                    hasIdentifier = !isKeyword;
                }

                if (hasIdentifier)
                {
                    items.Add(new ContextMenuItem
                    {
                        Label = "Go to Definition",
                        Shortcut = "F12",
                        Action = () => GoToDefinition(line, col)
                    });
                }
            }

            if (items.Count > 0)
            {
                _contextMenu.Show(screenX, screenY, items);
            }
        };
    }

    private async void GoToDefinition(int line, int col)
    {
        if (string.IsNullOrEmpty(_currentFilePath)) return;

        try
        {
            var result = await _lspService.GetDefinitionAsync(_currentFilePath, line, col);
            if (result.HasValue)
            {
                var (targetPath, targetLine, targetCol) = result.Value;

                // Reutiliza tab já aberta, ou reusa a única tab vazia, ou cria nova.
                var existingTab = _tabManager.Tabs.FirstOrDefault(t => t.FilePath == targetPath);
                if (existingTab != null)
                {
                    _tabManager.SelectTab(_tabManager.Tabs.IndexOf(existingTab));
                }
                else if (_tabManager.Tabs.Count == 1 && string.IsNullOrEmpty(_tabManager.Tabs[0].FilePath))
                {
                    var tab = _tabManager.Tabs[0];
                    tab.Buffer.LoadText(File.ReadAllText(targetPath));
                    tab.FilePath = targetPath;
                    tab.IsDirty = false;
                }
                else
                {
                    var newBuffer = new TextBuffer();
                    newBuffer.LoadText(File.ReadAllText(targetPath));
                    _tabManager.AddTab(newBuffer, new CursorManager(newBuffer), targetPath);
                }

                // Sincroniza todos os handlers com a nova aba antes de posicionar o cursor.
                SyncActiveTab(false);

                // Posiciona o cursor e centraliza o scroll.
                _cursor.SetPosition(targetLine, targetCol);
                _editor.EnsureCursorVisible();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GoToDefinition] Falha ao ir para definição: {ex.Message}");
        }
    }


    /// <summary>
    /// Shared by the keyboard and mouse tab-switch paths.
    /// </summary>
    /// <remarks>
    /// The outgoing tab's scroll position used to be saved only on the mouse path, so any tab last
    /// left via Ctrl+Tab restored at (0,0) instead of where the user had been reading.
    /// </remarks>
    private void SwitchTab(int direction)
    {
        SaveActiveTabScroll();

        if (direction > 0) _tabManager.NextTab();
        else _tabManager.PreviousTab();

        SyncActiveTab(false);
    }

    private void SaveActiveTabScroll()
    {
        var active = _tabManager.ActiveTab;
        if (active == null) return;

        active.SavedScrollX = _editor.ScrollX;
        active.SavedScrollY = _editor.ScrollY;
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
        _surface?.Dispose();
        _surface = null!;

        if (_grContext == null) return;

        var width = Math.Max(1, _window.Size.X);
        var height = Math.Max(1, _window.Size.Y);

        // The render target wraps the default framebuffer and is unmanaged; not disposing it leaked
        // a native allocation on every window resize.
        using var target = new GRBackendRenderTarget(width, height, 0, 8, new GRGlFramebufferInfo(0, 0x8058)); // 0x8058 = GL_RGBA8

        _surface = SKSurface.Create(_grContext, target, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888)
                   ?? throw new InvalidOperationException($"Could not create a {width}x{height} Skia surface.");

        //StartupLog.Write("SetupSurface: created", $"{width}x{height}");
    }

    private static readonly List<EditorTheme> AvailableThemes =
    [
        EditorTheme.Dark,
        EditorTheme.Midnight,
        EditorTheme.Nordic,
        EditorTheme.Light,
        EditorTheme.Sepia,
        EditorTheme.Rose
    ];

    public void Dispose()
    {
        _gitHubService?.Dispose();

        _diagnosticCts?.Cancel();
        _diagnosticCts?.Dispose();
        _diagnosticCts = null;

        _lspService.Dispose();
        _terminal.ShutdownAllTerminals();

        _surface?.Dispose();
        _grContext?.Dispose();
        _window?.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>Schedule an action on the main/render thread.</summary>
    private void InvokeOnUi(Action action)
    {
        // Fire-and-forget: simple pattern works because only one action is queued at a time.
        Interlocked.CompareExchange(ref _pendingAction, action, null);
    }

    /// <summary>Refresh explorer with git status when repo state changes.</summary>
    private void RefreshExplorerWithGit()
    {
        // Stub — will call _explorer.RefreshWithGitStatus() once that method exists.
        _explorer.Refresh();
    }

    /// <summary>Connect to git repository if path contains a .git directory.</summary>
    private async Task ConnectToRepository(string path)
    {
        try
        {
            var gitDir = Path.Combine(path, ".git");
            if (!Directory.Exists(gitDir)) return;

            await _gitHubService.ConnectToRepositoryAsync(path);

            // UI update happens via ConnectionStateChanged event → InvokeOnUi
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GitHub] Failed to connect to repo at {path}: {ex.Message}");
        }
    }

    /// <summary>Open branch selector overlay (Fase 3).</summary>
    private void ToggleBranchSelector()
    {
        if (_branchSelector.IsVisible)
        {
            _branchSelector.IsVisible = false;
            return;
        }

        string? currentBranch = _gitHubService.State.CurrentBranch;

        // Wire up selection callback
        _branchSelector.OnBranchSelected = async branchName =>
        {
            try
            {
                await _gitHubService.SwitchBranchAsync(branchName);
            }
            catch (Exception ex)
            {
                _modal.Show("Erro", $"Falha ao alternar branch '{branchName}': {ex.Message}", null, null, null);
            }
        };

        Task.Run(async () =>
        {
            var local = await _gitHubService.ListLocalBranchesAsync();
            var remote = await _gitHubService.ListRemoteBranchesAsync("origin");

            InvokeOnUi(() =>
            {
                _branchSelector.CurrentBranchName = currentBranch;
                _branchSelector.LocalBranches = local.Select(b => b.Name).ToList();
                _branchSelector.RemoteBranches = remote;
                _branchSelector.ResetState();
                // Compute bounds synchronously for immediate hit-testing (click/wheel)
                _branchSelector.ComputeBounds(new SKRect(0, 0, _window.Size.X, _window.Size.Y));
                _branchSelector.IsVisible = true;
            });
        });
    }

    public void Run() => _window.Run();
}