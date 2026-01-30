using SLText.Core.Commands;
using SLText.Core.Interfaces;
using TextCopy;

namespace SLText.Core.Engine;

public class InputHandler
{
    private readonly CursorManager _cursor;
    private readonly TextBuffer _buffer;
    private readonly UndoManager _undoManager;
    private readonly Dictionary<(bool ctrl, bool shift, string key), ICommand> _shortcuts = new();
    private SaveFileCommand _saveCommand;
    private string _lastDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    
    private TypingCommand? _currentTypingCommand;
    
    public event Action<float, float>? OnScrollRequested;
    
    private readonly Dictionary<char, char> _pairs = new()
    {
        { '(', ')' },
        { '[', ']' },
        { '{', '}' },
        { '"', '"' },
        { '\'', '\'' }
    };
    
    public InputHandler(CursorManager cursor, TextBuffer buffer, UndoManager undoManager, IDialogService dialogs, Action<string, bool> onFileAction)
    {
        _cursor = cursor;
        _buffer = buffer;
        _undoManager = undoManager;
        
        // move com control
        _shortcuts.Add((true, false, "RightArrow"), new MoveWordRightCommand(_cursor, _buffer));
        _shortcuts.Add((true, false, "LeftArrow"), new MoveWordLeftCommand(_cursor, _buffer));
        
        _shortcuts.Add((true, true, "RightArrow"), new MoveWordRightCommand(_cursor, _buffer));
        _shortcuts.Add((true, true, "LeftArrow"), new MoveWordLeftCommand(_cursor, _buffer));
        
        _shortcuts.Add((false, false, "Tab"), new InsertTabCommand(_buffer, _cursor));
        _shortcuts.Add((true, false, "Backspace"), new DeleteWordLeftCommand(_cursor, _buffer));
        
        _shortcuts.Add((true, false, "UpArrow"), new MoveFourLinesUpCommand(_cursor));
        _shortcuts.Add((true, false, "DownArrow"), new MoveFourLinesDownCommand(_cursor));
        
        _shortcuts.Add((true, false, "O"), new OpenFileCommand(dialogs, buffer, (path) => onFileAction(path, true), () => _lastDirectory));
        _saveCommand = new SaveFileCommand(dialogs, _buffer, (path) => onFileAction(path, false), () => _lastDirectory);    }
    
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

        if (char.IsWhiteSpace(c) || char.IsPunctuation(c))
        {
            _currentTypingCommand.FinalizeCommand(); 
            _undoManager.AddExternalCommand(_currentTypingCommand); 
            _currentTypingCommand = null;
        }
    }

    public void HandleShortcut(bool ctrl, bool shift, string key)
    {
        if (_currentTypingCommand != null)
        {
            _currentTypingCommand.FinalizeCommand();
            _undoManager.AddExternalCommand(_currentTypingCommand); 
            _currentTypingCommand = null;
        }
        
        bool isMovement = IsMovementKey(key);
        if (isMovement)
        {
            if (shift) _cursor.StartSelection();
            else _cursor.ClearSelection();
        }
        
        if (ctrl && !shift && key == "A")
        {
            _cursor.SelectAll();
            return;
        }
        
        if (ctrl && !shift && key == "X") 
        { 
            HandleCut(); 
            return; 
        }
        
        if (ctrl && !shift && key == "Z") { _undoManager.Undo(); return; }
        if (ctrl && !shift && key == "Y") { _undoManager.Redo(); return; }
        
        if (_cursor.HasSelection && IsDestructiveKey(key))
        {
            DeleteSelectedText();
            if (key == "Backspace" || key == "Delete") return; 
        }
        
        if (!ctrl) 
        {
            switch (key)
            {
                case "UpArrow":    _cursor.MoveUp(); return;
                case "DownArrow":  _cursor.MoveDown(); return;
                case "LeftArrow":  _cursor.MoveLeft(); return;
                case "RightArrow": _cursor.MoveRight(); return;
                case "Enter":      if(!shift) { _cursor.Enter(); return; } break;
                case "Backspace":  if(!shift) { _cursor.Backspace(); return; } break;
                case "Delete":     _cursor.Delete(); return;
            }
        }
        
        var lookup = (ctrl, shift, key);
        if (_shortcuts.TryGetValue(lookup, out var command))
        {
            _undoManager.ExecuteCommand(command);
        }
    }
    
    public void HandlePaste(string text)
    {
        ResetTypingState();
        
        if (_cursor.HasSelection)
        {
            DeleteSelectedText(); 
        }

        var pasteCmd = new PasteCommand(_buffer, _cursor, text);
        _undoManager.ExecuteCommand(pasteCmd);
       
    }
    
    public void HandleCut()
    {
        if (!_cursor.HasSelection) return;

        HandleCopy();
        DeleteSelectedText();
    }
    
    public void ResetTypingState()
    {
        if (_currentTypingCommand != null)
        {
            _currentTypingCommand.FinalizeCommand();
            _currentTypingCommand = null;
        }
    }
    
    private bool IsMovementKey(string key) => 
        key.Contains("Arrow") || key == "Home" || key == "End" || key == "PageUp" || key == "PageDown";

    private bool IsDestructiveKey(string key) => 
        key == "Backspace" || key == "Delete" || key == "Enter" || key == "Tab";
    
    private void DeleteSelectedText()
    {
        var deleteCmd = new DeleteSelectionCommand(_buffer, _cursor);
        _undoManager.ExecuteCommand(deleteCmd);
        _cursor.ClearSelection();
    }
    
    public void HandleCopy()
    {
        
        var range = _cursor.GetSelectionRange();
        if (range == null) return;

        var (sLine, sCol, eLine, eCol) = range.Value;
    
        // Extrai o texto do buffer
        List<string> selectedTextLines = new();
        var lines = _buffer.GetLines().ToList();

        if (sLine == eLine)
        {
            selectedTextLines.Add(lines[sLine].Substring(sCol, eCol - sCol));
        }
        else
        {
            // Primeira linha
            selectedTextLines.Add(lines[sLine].Substring(sCol));
            // Linhas do meio
            for (int i = sLine + 1; i < eLine; i++)
                selectedTextLines.Add(lines[i]);
            // Última linha
            selectedTextLines.Add(lines[eLine].Substring(0, eCol));
        }

        string fullText = string.Join(Environment.NewLine, selectedTextLines);
    
        try {
            ClipboardService.SetText(fullText);
        } catch {  }
    }
    
    public void UpdateCurrentPath(string path)
    {
        _saveCommand.SetPath(path);
    }
    
    public void HandleMouseScroll(float deltaY, bool ctrl, bool shift)
    {
        float scrollSpeed = 60f;

        if (shift)
        {
            OnScrollRequested?.Invoke(deltaY * scrollSpeed, 0); 
        }
        
        else
        {
            OnScrollRequested?.Invoke(0, deltaY * scrollSpeed);
        }
    }
    
    public void UpdateLastDirectory(string path)
    {
        if (!string.IsNullOrEmpty(path))
            _lastDirectory = Path.GetDirectoryName(path) ?? _lastDirectory;
    }
}