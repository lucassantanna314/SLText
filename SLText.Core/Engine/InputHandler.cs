using SLText.Core.Commands;
using SLText.Core.Interfaces;
using TextCopy;

namespace SLText.Core.Engine;

public class InputHandler
{
    private CursorManager _cursor;
    private TextBuffer _buffer;
    private readonly UndoManager _undoManager;
    
    private readonly Dictionary<(bool ctrl, bool shift, string key), Func<ICommand>> _undoableShortcuts = new();
    private readonly Dictionary<(bool ctrl, bool shift, string key), Func<ICommand>> _immediateShortcuts = new();
    
    private SaveFileCommand _saveCommand;
    private string _lastDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private readonly IDialogService _dialogs;
    private TypingCommand? _currentTypingCommand;
    private string? _currentFilePath;
    private readonly Action<string?, bool> _onFileAction;

    public event Action<float, float>? OnScrollRequested;
    public event Action<float>? OnZoomRequested;
    
    public event Action? OnTabCloseRequested;
    public event Action? OnNextTabRequested;
    public event Action? OnPreviousTabRequested;
    
    private readonly Dictionary<char, char> _pairs = new()
    {
        { '(', ')' }, { '[', ']' }, { '{', '}' }, { '"', '"' }, { '\'', '\'' }
    };
    
    public InputHandler(
        CursorManager cursor, 
        TextBuffer buffer, 
        UndoManager undoManager, 
        IDialogService dialogs, 
        Func<bool> getIsDirty, 
        Action<string?, bool> onFileAction,
        Action onSearchRequested)
    {
        _cursor = cursor;
        _buffer = buffer;
        _undoManager = undoManager;
        _dialogs = dialogs;
        _onFileAction = onFileAction;
        
        // Inicializa o SaveCommand com a aba atual
        _saveCommand = new SaveFileCommand(dialogs, _buffer, (path) => _onFileAction(path, false), () => _lastDirectory);

        // --- COMANDOS IMEDIATOS ---
        _immediateShortcuts.Add((true, false, "RightArrow"), () => new MoveWordRightCommand(_cursor, _buffer));
        _immediateShortcuts.Add((true, false, "LeftArrow"), () => new MoveWordLeftCommand(_cursor, _buffer));
        _immediateShortcuts.Add((true, true, "RightArrow"), () => new MoveWordRightCommand(_cursor, _buffer));
        _immediateShortcuts.Add((true, true, "LeftArrow"), () => new MoveWordLeftCommand(_cursor, _buffer));
        _immediateShortcuts.Add((true, false, "UpArrow"), () => new MoveFourLinesUpCommand(_cursor));
        _immediateShortcuts.Add((true, false, "DownArrow"), () => new MoveFourLinesDownCommand(_cursor));
        _immediateShortcuts.Add((true, false, "L"), () => new SelectLineCommand(_cursor, _buffer));
        
        _immediateShortcuts.Add((true, false, "S"), () => _saveCommand);
        
        _immediateShortcuts.Add((true, false, "O"), () => new OpenFileCommand(
            _dialogs, 
            _buffer, 
            (path, isOpening) => _onFileAction(path, isOpening), 
            () => _lastDirectory, 
            _saveCommand, 
            getIsDirty, 
            _undoManager));
        
        _immediateShortcuts.Add((true, false, "N"), () => new NewFileCommand(_buffer, _cursor, dialogs, _saveCommand, getIsDirty, _onFileAction, _undoManager));
        _immediateShortcuts.Add((true, false, "F"), () => new SearchTriggerCommand(onSearchRequested));
        
        // Tabs
        _immediateShortcuts.Add((true, false, "W"), () => new AnonymousCommand(() => OnTabCloseRequested?.Invoke()));
        
        _immediateShortcuts.Add((true, false, "Tab"), () => new AnonymousCommand(() => OnNextTabRequested?.Invoke()));
        
        _immediateShortcuts.Add((true, true, "Tab"), () => new AnonymousCommand(() => OnPreviousTabRequested?.Invoke()));
        
        // --- COMANDOS COM HISTÓRICO ---
        _undoableShortcuts.Add((false, false, "Tab"), () => new InsertTabCommand(_buffer, _cursor));
        _undoableShortcuts.Add((false, false, "Enter"), () => new EnterCommand(_buffer, _cursor, _currentFilePath));        
        _undoableShortcuts.Add((false, false, "Backspace"), () => new BackspaceCommand(_buffer, _cursor));
        _undoableShortcuts.Add((false, false, "Delete"), () => new DeleteCommand(_buffer, _cursor));
        _undoableShortcuts.Add((true, false, "Backspace"), () => new DeleteWordLeftCommand(_cursor, _buffer));
        _undoableShortcuts.Add((true, true, "K"), () => new DeleteLineCommand(_buffer, _cursor));
        _undoableShortcuts.Add((true, true, "UpArrow"), () => new MoveLineCommand(_buffer, _cursor, -1));
        _undoableShortcuts.Add((true, true, "DownArrow"), () => new MoveLineCommand(_buffer, _cursor, 1));
        _undoableShortcuts.Add((true, false, "D"), () => new DuplicateLineCommand(_buffer, _cursor));
    }
    
    public void UpdateActiveData(CursorManager newCursor, TextBuffer newBuffer)
    {
        FinalizeTypingState();
        _cursor = newCursor;
        _buffer = newBuffer;
        
        _saveCommand = new SaveFileCommand(_dialogs, _buffer, (path) => _onFileAction(path, false), () => _lastDirectory);
        if (_currentFilePath != null) _saveCommand.SetPath(_currentFilePath);
    }
    
    public void HandleTextInput(char c)
    {
        if (_cursor.HasSelection) DeleteSelectedText();
    
        if (_currentTypingCommand == null)
        {
            _currentTypingCommand = new TypingCommand(_buffer, _cursor);
        }
    
        _cursor.Insert(c);

        if (_pairs.TryGetValue(c, out char closingChar))
        {
            _cursor.Insert(closingChar);
            _cursor.MoveLeft();
        }
    }

    public void HandleShortcut(bool ctrl, bool shift, string key)
    {
        var lookup = (ctrl, shift, key);
        bool isSpecialKey = IsDestructiveKey(key) || IsMovementKey(key) || 
                            _undoableShortcuts.ContainsKey(lookup) ||
                            _immediateShortcuts.ContainsKey(lookup); 
        
        if (!ctrl && !isSpecialKey && key.Length == 1) return;

        FinalizeTypingState();

        if (IsMovementKey(key))
        {
            if (shift) _cursor.StartSelection();
            else if (!ctrl) _cursor.ClearSelection();
        }

        if (ctrl && !shift && key == "A") { _cursor.SelectAll(); return; }
        if (ctrl && !shift && key == "X") { HandleCut(); return; }
        if (ctrl && !shift && key == "Z") { _undoManager.Undo(); return; }
        if (ctrl && !shift && key == "Y") { _undoManager.Redo(); return; }
        
        if (_cursor.HasSelection && IsDestructiveKey(key))
        {
            DeleteSelectedText();
            if (key == "Backspace" || key == "Delete") return; 
        }

        if (_immediateShortcuts.TryGetValue(lookup, out var immediateFactory))
        {
            immediateFactory().Execute(); 
            return; 
        }

        if (_undoableShortcuts.TryGetValue(lookup, out var commandFactory))
        {
            _undoManager.ExecuteCommand(commandFactory());
            return; 
        }

        if (!ctrl) 
        {
            switch (key)
            {
                case "UpArrow":    _cursor.MoveUp(); break;
                case "DownArrow":  _cursor.MoveDown(); break;
                case "LeftArrow":  _cursor.MoveLeft(); break;
                case "RightArrow": _cursor.MoveRight(); break;
            }
        }
    }
    
    public void HandlePaste(string text)
    {
        FinalizeTypingState();
        if (_cursor.HasSelection) DeleteSelectedText(); 
        _undoManager.ExecuteCommand(new PasteCommand(_buffer, _cursor, text));
    }
    
    public void HandleCut()
    {
        if (!_cursor.HasSelection) 
        {
            _undoManager.ExecuteCommand(new DeleteLineCommand(_buffer, _cursor));
            return;
        }
        HandleCopy();
        DeleteSelectedText();
    }
    
    public void ResetTypingState() => FinalizeTypingState();
    
    private bool IsMovementKey(string key) => 
        key.Contains("Arrow") || key == "Up" || key == "Down" || key == "Left" || key == "Right" ||
        key == "Home" || key == "End" || key == "PageUp" || key == "PageDown";

    private bool IsDestructiveKey(string key) => 
        key == "Backspace" || key == "Delete" || key == "Enter" || key == "Tab";
    
    private void DeleteSelectedText()
    {
        _undoManager.ExecuteCommand(new DeleteSelectionCommand(_buffer, _cursor));
        _cursor.ClearSelection();
    }
    
    public void HandleCopy()
    {
        var range = _cursor.GetSelectionRange();
        if (range == null) return;
        var (sLine, sCol, eLine, eCol) = range.Value;
        var lines = _buffer.GetLines().ToList();

        string fullText;
        if (sLine == eLine) fullText = lines[sLine].Substring(sCol, eCol - sCol);
        else 
        {
            var selectedLines = new List<string> { lines[sLine].Substring(sCol) };
            for (int i = sLine + 1; i < eLine; i++) selectedLines.Add(lines[i]);
            selectedLines.Add(lines[eLine].Substring(0, eCol));
            fullText = string.Join(Environment.NewLine, selectedLines);
        }

        try { ClipboardService.SetText(fullText); } catch { }
    }
    
    public void UpdateCurrentPath(string path)
    {
        _currentFilePath = path;
        _saveCommand.SetPath(path);
    }
    
    public void HandleMouseScroll(float deltaY, bool ctrl, bool shift)
    {
        if (ctrl) OnZoomRequested?.Invoke(deltaY > 0 ? 1f : -1f);
        else 
        {
            float speed = 60f;
            if (shift) OnScrollRequested?.Invoke(deltaY * speed, 0);
            else OnScrollRequested?.Invoke(0, deltaY * speed);
        }
    }
    
    public void UpdateLastDirectory(string path)
    {
        if (!string.IsNullOrEmpty(path)) _lastDirectory = Path.GetDirectoryName(path) ?? _lastDirectory;
    }
    
    public void AddEditorShortcuts(IZoomable zoomable)
    {
        _immediateShortcuts[(true, false, "0")] = () => new ResetZoomCommand(zoomable);
    }
    
    public void FinalizeTypingState()
    {
        if (_currentTypingCommand != null)
        {
            _currentTypingCommand.FinalizeCommand();
            _undoManager.AddExternalCommand(_currentTypingCommand); 
            _currentTypingCommand = null;
        }
    }
    
    public IDialogService GetDialogService() => _dialogs;
}