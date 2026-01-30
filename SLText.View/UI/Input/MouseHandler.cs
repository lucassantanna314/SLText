using Silk.NET.Input;
using SLText.Core.Engine;
using SLText.View.Components;

namespace SLText.View.UI.Input;

public class MouseHandler
{
    private readonly EditorComponent _editor;
    private readonly CursorManager _cursor;
    private readonly InputHandler _inputHandler;
    private readonly IInputContext _inputContext;
    private bool _isMouseDown;

    public MouseHandler(EditorComponent editor, CursorManager cursor, InputHandler inputHandler, IInputContext inputContext)
    {
        _editor = editor;
        _cursor = cursor;
        _inputHandler = inputHandler;
        _inputContext = inputContext;
    }

    public void OnMouseDown(IMouse mouse, MouseButton button)
    {
        _inputHandler.ResetTypingState();
        
        if (button == MouseButton.Left)
        {
            _isMouseDown = true;
            var pos = mouse.Position;
        
            if (_editor.Bounds.Contains(pos.X, pos.Y))
            {
                var (line, col) = _editor.GetTextPositionFromMouse(pos.X, pos.Y);
            
                _inputHandler.HandleShortcut(false, false, "None"); 
                
                _cursor.ClearSelection();
                _cursor.SetPosition(line, col);
                _cursor.StartSelection();
            }
        }
    }

    public void OnMouseMove(IMouse mouse, System.Numerics.Vector2 position)
    {
        if (_isMouseDown)
        {
            if (_editor.Bounds.Contains(position.X, position.Y))
            {
                var (line, col) = _editor.GetTextPositionFromMouse(position.X, position.Y);
                _cursor.SetPosition(line, col);
                _editor.RequestScrollToCursor();
            }
        }
    }

    public void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (button == MouseButton.Left)
        {
            _isMouseDown = false;
        
            var range = _cursor.GetSelectionRange(); 
            if (range != null && range.Value.startLine == range.Value.endLine && 
                range.Value.startCol == range.Value.endCol)
            {
                _cursor.ClearSelection();
            }
        }
    }

    public void OnMouseScroll(IMouse mouse, ScrollWheel scroll)
    {
        var keyboard = _inputContext.Keyboards[0];
        bool ctrl = keyboard.IsKeyPressed(Key.ControlLeft) || keyboard.IsKeyPressed(Key.ControlRight);

        _inputHandler.HandleMouseScroll(scroll.Y, ctrl);
    }
}