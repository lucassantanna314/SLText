using SLText.Core.Commands;
using SLText.Core.Interfaces;
using TextCopy;

namespace SLText.Core.Engine;

public class InputHandler
{
    private readonly CursorManager _cursor;
    private readonly TextBuffer _buffer;
    private readonly UndoManager _undoManager;
    
    private readonly Dictionary<(bool ctrl, bool shift, string key), Func<ICommand>> _undoableShortcuts = new();
    private readonly Dictionary<(bool ctrl, bool shift, string key), ICommand> _immediateShortcuts = new();
    
    private SaveFileCommand _saveCommand;
    private string _lastDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private readonly IDialogService _dialogs;
    private TypingCommand? _currentTypingCommand;
    private string? _currentFilePath;
    
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
        
        // --- COMANDOS IMEDIATOS (Navegação e Sistema - Sem Undo) ---
        
        _immediateShortcuts.Add((true, false, "RightArrow"), new MoveWordRightCommand(_cursor, _buffer));
        _immediateShortcuts.Add((true, false, "LeftArrow"), new MoveWordLeftCommand(_cursor, _buffer));
        _immediateShortcuts.Add((true, true, "RightArrow"), new MoveWordRightCommand(_cursor, _buffer));
        _immediateShortcuts.Add((true, true, "LeftArrow"), new MoveWordLeftCommand(_cursor, _buffer));
        _immediateShortcuts.Add((true, false, "UpArrow"), new MoveFourLinesUpCommand(_cursor));
        _immediateShortcuts.Add((true, false, "DownArrow"), new MoveFourLinesDownCommand(_cursor));
        _immediateShortcuts.Add((true, false, "L"), new SelectLineCommand(_cursor, _buffer));

        _saveCommand = new SaveFileCommand(dialogs, _buffer, (path) => onFileAction(path, false), () => _lastDirectory);  
        _immediateShortcuts.Add((true, false, "S"), _saveCommand);
        _immediateShortcuts.Add((true, false, "O"), new OpenFileCommand(dialogs, buffer, (path) => onFileAction(path, true), () => _lastDirectory, _saveCommand, getIsDirty, _undoManager));
        
        var newFileCmd = new NewFileCommand(_buffer, _cursor, dialogs, _saveCommand, getIsDirty, onFileAction, _undoManager);
        _immediateShortcuts.Add((true, false, "N"), newFileCmd);


        // --- COMANDOS COM HISTÓRICO (Factories para Estabilidade do Undo) ---
        _undoableShortcuts.Add((false, false, "Tab"), () => new InsertTabCommand(_buffer, _cursor));
        _undoableShortcuts.Add((false, false, "Enter"), () => new EnterCommand(_buffer, _cursor, _currentFilePath));        
        // Deletação
        _undoableShortcuts.Add((false, false, "Backspace"), () => new BackspaceCommand(_buffer, _cursor));
        _undoableShortcuts.Add((false, false, "Delete"), () => new DeleteCommand(_buffer, _cursor));
        _undoableShortcuts.Add((true, false, "Backspace"), () => new DeleteWordLeftCommand(_cursor, _buffer));
        _undoableShortcuts.Add((true, true, "K"), () => new DeleteLineCommand(_buffer, _cursor));
    
        // Linhas
        _undoableShortcuts.Add((true, true, "UpArrow"), () => new MoveLineCommand(_buffer, _cursor, -1));
        _undoableShortcuts.Add((true, true, "DownArrow"), () => new MoveLineCommand(_buffer, _cursor, 1));
        _undoableShortcuts.Add((true, false, "D"), () => new DuplicateLineCommand(_buffer, _cursor));
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
        bool isSpecialKey = IsDestructiveKey(key) || IsMovementKey(key) || 
                            _undoableShortcuts.ContainsKey((ctrl, shift, key)) ||
                            _immediateShortcuts.ContainsKey((ctrl, shift, key)); 
        
        if (!ctrl && !isSpecialKey && key.Length == 1) return;

        FinalizeTypingState();

        // 1. Gerenciar Seleção
        bool isMovement = IsMovementKey(key);
        if (isMovement)
        {
            if (shift) _cursor.StartSelection();
            else if (!ctrl) _cursor.ClearSelection();
        }

        // 2. Atalhos Globais
        if (ctrl && !shift && key == "A") { _cursor.SelectAll(); return; }
        if (ctrl && !shift && key == "X") { HandleCut(); return; }
        if (ctrl && !shift && key == "Z") { _undoManager.Undo(); return; }
        if (ctrl && !shift && key == "Y") { _undoManager.Redo(); return; }
        
        // 3. Deletar seleção
        if (_cursor.HasSelection && IsDestructiveKey(key))
        {
            DeleteSelectedText();
            if (key == "Backspace" || key == "Delete") return; 
        }
        
        var lookup = (ctrl, shift, key);

        // Verifica Comandos Imediatos (Navegação/Sistema) - Execute DIRETO
        if (_immediateShortcuts.TryGetValue(lookup, out var immediateCmd))
        {
            immediateCmd.Execute(); 
            return; 
        }

        // Verifica Comandos de Edição - Passa para o UndoManager
     
        if (_undoableShortcuts.TryGetValue(lookup, out var commandFactory))
        {
            var cmd = commandFactory(); 
            _undoManager.ExecuteCommand(cmd);
            return; 
        }

        // Movimentação Simples e Edição Direta
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
        _undoManager.ExecuteCommand(new DeleteSelectionCommand(_buffer, _cursor));
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
        _currentFilePath = path;
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
        _immediateShortcuts[(true, false, "0")] = new ResetZoomCommand(zoomable);
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