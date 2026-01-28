using SLText.Core.Commands;
using SLText.Core.Interfaces;

namespace SLText.Core.Engine;

public class InputHandler
{
    private readonly CursorManager _cursor;
    private readonly TextBuffer _buffer;
    private readonly UndoManager _undoManager;
    private readonly Dictionary<(bool ctrl, bool shift, string key), ICommand> _shortcuts = new();
    
    private TypingCommand? _currentTypingCommand;
    
    public InputHandler(CursorManager cursor, TextBuffer buffer, UndoManager undoManager)
    {
        _cursor = cursor;
        _buffer = buffer;
        _undoManager = undoManager;

        _shortcuts.Add((true, false, "RightArrow"), new MoveWordRightCommand(_cursor, _buffer));
        _shortcuts.Add((true, false, "LeftArrow"), new MoveWordLeftCommand(_cursor, _buffer));
        
        _shortcuts.Add((true, false, "S"), new SaveCommand());
    }
    
    public void HandleTextInput(char c)
    {
        if (_currentTypingCommand == null)
        {
            _currentTypingCommand = new TypingCommand(_buffer, _cursor);
            _undoManager.ExecuteCommand(_currentTypingCommand);
        }

        _cursor.Insert(c);

        if (char.IsWhiteSpace(c))
        {
            _currentTypingCommand.FinalizeCommand();
            _currentTypingCommand = null;
        }
    }

    public void HandleShortcut(bool ctrl, bool shift, string key)
    {
        if (_currentTypingCommand != null)
        {
            _currentTypingCommand.FinalizeCommand();
            _currentTypingCommand = null;
        }
        
        if (ctrl && !shift && key == "Z") { _undoManager.Undo(); return; }
        if (ctrl && !shift && key == "Y") { _undoManager.Redo(); return; }
        
        if (!ctrl && !shift)
        {
            switch (key)
            {
                case "UpArrow":    _cursor.MoveUp(); return;
                case "DownArrow":  _cursor.MoveDown(); return;
                case "LeftArrow":  _cursor.MoveLeft(); return;
                case "RightArrow": _cursor.MoveRight(); return;
                case "Enter":      _cursor.Enter(); return;
                case "Backspace":  _cursor.Backspace(); return;
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
        if (_currentTypingCommand != null)
        {
            _currentTypingCommand.FinalizeCommand();
            _currentTypingCommand = null;
        }

        var pasteCmd = new PasteCommand(_buffer, _cursor, text);
        _undoManager.ExecuteCommand(pasteCmd);
    }
}