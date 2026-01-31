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
    private readonly IDialogService _dialogs;
    private TypingCommand? _currentTypingCommand;
    
    public event Action<float, float>? OnScrollRequested;
    public event Action<float>? OnZoomRequested;
    
    private readonly Dictionary<char, char> _pairs = new()
    {
        { '(', ')' },
        { '[', ']' },
        { '{', '}' },
        { '"', '"' },
        { '\'', '\'' }
    };
    
    public InputHandler(
        CursorManager cursor, 
        TextBuffer buffer, 
        UndoManager undoManager, 
        IDialogService dialogs, 
        Func<bool> getIsDirty, 
        Action<string?, bool> onFileAction)
    
    {
        _cursor = cursor;
        _buffer = buffer;
        _undoManager = undoManager;
        _dialogs = dialogs;
        
        // move com control
        _shortcuts.Add((true, false, "RightArrow"), new MoveWordRightCommand(_cursor, _buffer));
        _shortcuts.Add((true, false, "LeftArrow"), new MoveWordLeftCommand(_cursor, _buffer));
        
        _shortcuts.Add((true, true, "RightArrow"), new MoveWordRightCommand(_cursor, _buffer));
        _shortcuts.Add((true, true, "LeftArrow"), new MoveWordLeftCommand(_cursor, _buffer));
        
        _shortcuts.Add((false, false, "Tab"), new InsertTabCommand(_buffer, _cursor));
        _shortcuts.Add((true, false, "Backspace"), new DeleteWordLeftCommand(_cursor, _buffer));
        
        _shortcuts.Add((true, false, "UpArrow"), new MoveFourLinesUpCommand(_cursor));
        _shortcuts.Add((true, false, "DownArrow"), new MoveFourLinesDownCommand(_cursor));
        
        _shortcuts.Add((true, true, "UpArrow"), new MoveLineCommand(_buffer, _cursor, -1));
        _shortcuts.Add((true, true, "DownArrow"), new MoveLineCommand(_buffer, _cursor, 1));
        
        _shortcuts.Add((true, true, "K"), new DeleteLineCommand(_buffer, _cursor));
        _shortcuts.Add((true, false, "L"), new SelectLineCommand(_cursor, _buffer));
        
        _shortcuts.Add((true, false, "D"), new DuplicateLineCommand(_buffer, _cursor));
        
        _saveCommand = new SaveFileCommand(dialogs, _buffer, (path) => onFileAction(path, false), () => _lastDirectory);  
        _shortcuts.Add((true, false, "S"), _saveCommand);
        
        _shortcuts.Add((true, false, "O"), new OpenFileCommand(
            dialogs, 
            buffer, 
            (path) => onFileAction(path, true), 
            () => _lastDirectory,
            _saveCommand, 
            getIsDirty    
        ));
        
        
        var newFileCmd = new NewFileCommand(
            _buffer, 
            _cursor, 
            dialogs, 
            _saveCommand, 
            getIsDirty, 
            onFileAction,
            _undoManager
        );
        
        _shortcuts.Add((true, false, "N"), newFileCmd);
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

    // 1. Gerenciar Seleção antes do movimento
    bool isMovement = IsMovementKey(key);
    if (isMovement)
    {
        // Se shift está pressionado (independente de ctrl), marcamos o início se não houver
        if (shift) 
        {
            _cursor.StartSelection();
        }
        else if (!ctrl) // Se não tem shift nem ctrl, limpamos
        {
            _cursor.ClearSelection();
        }
    }

    // 2. Atalhos Globais
    if (ctrl && !shift && key == "A") { _cursor.SelectAll(); return; }
    if (ctrl && !shift && key == "X") { HandleCut(); return; }
    if (ctrl && !shift && key == "Z") { _undoManager.Undo(); return; }
    if (ctrl && !shift && key == "Y") { _undoManager.Redo(); return; }
    
    // 3. Deletar seleção se for tecla destrutiva
    if (_cursor.HasSelection && IsDestructiveKey(key))
    {
        DeleteSelectedText();
        if (key == "Backspace" || key == "Delete") return; 
    }
    
    // 4. Movimentação e Atalhos de Dicionário
    var lookup = (ctrl, shift, key);
    if (_shortcuts.TryGetValue(lookup, out var command))
    {
        _undoManager.ExecuteCommand(command);
        // Se executou um atalho de movimento (como Ctrl+Seta ou Ctrl+Shift+Seta), 
        // damos return para não mover de novo no switch abaixo
        return; 
    }

    // 5. Movimentação Simples (Só entra aqui se não for um atalho de dicionário)
    if (!ctrl) 
    {
        switch (key)
        {
            case "UpArrow":
            case "Up":         _cursor.MoveUp(); break;
            case "DownArrow":
            case "Down":       _cursor.MoveDown(); break;
            case "LeftArrow":
            case "Left":       _cursor.MoveLeft(); break;
            case "RightArrow":
            case "Right":      _cursor.MoveRight(); break;
            case "Enter":      if(!shift) _cursor.Enter(); return;
            case "Backspace":  if(!shift) _cursor.Backspace(); return;
            case "Delete":     _cursor.Delete(); return;
        }
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
        if (!_cursor.HasSelection) 
        {
            _undoManager.ExecuteCommand(new DeleteLineCommand(_buffer, _cursor));
            return;
        }

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
    
    private bool IsMovementKey(string key) 
    {
        return key.Contains("Arrow") || 
               key == "Up" || key == "Down" || key == "Left" || key == "Right" ||
               key == "Home" || key == "End" || key == "PageUp" || key == "PageDown";
    }

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
        
        if (ctrl)
        {
            float zoomAmount = deltaY > 0 ? 1f : -1f;
        
            OnZoomRequested?.Invoke(zoomAmount);
        }
        else
        {
            float scrollSpeed = 60f;
            if (shift) OnScrollRequested?.Invoke(deltaY * scrollSpeed, 0);
            else OnScrollRequested?.Invoke(0, deltaY * scrollSpeed);
        }
    }
    
    public void UpdateLastDirectory(string path)
    {
        if (!string.IsNullOrEmpty(path))
            _lastDirectory = Path.GetDirectoryName(path) ?? _lastDirectory;
    }
    
    public void AddEditorShortcuts(IZoomable zoomable)
    {
        _shortcuts[(true, false, "0")] = new ResetZoomCommand(zoomable);
    }
    
    public IDialogService GetDialogService() => _dialogs;
}