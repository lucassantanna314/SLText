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
            _currentTypingCommand = null;
        }
    }

    public void HandleShortcut(bool ctrl, bool shift, string key)
    {
        _currentTypingCommand = null;
        
        if (ctrl && !shift && key == "Z")
        {
            _undoManager.Undo();
            return;
        }
        
        if (ctrl && !shift && key == "Y")
        {
            _undoManager.Redo();
            return;
        }
        
        var lookup = (ctrl, shift, key);

        if (_shortcuts.TryGetValue(lookup, out var command))
        {
            _undoManager.ExecuteCommand(command);
        }
    }
}