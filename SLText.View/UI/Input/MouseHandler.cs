using Silk.NET.Input;
using SLText.Core.Engine;
using SLText.View.Components;
using SLText.View.Components.Canvas;

namespace SLText.View.UI.Input;

public class MouseHandler
{
    private readonly EditorComponent _editor;
    private readonly CursorManager _cursor;
    private readonly InputHandler _inputHandler;
    private readonly IInputContext _inputContext;
    private readonly ModalComponent _modal;
    private bool _isMouseDown;

    public MouseHandler(
        EditorComponent editor, 
        CursorManager cursor, 
        InputHandler inputHandler, 
        IInputContext inputContext,
        ModalComponent modal) 
    {
        _editor = editor;
        _cursor = cursor;
        _inputHandler = inputHandler;
        _inputContext = inputContext;
        _modal = modal;
    }

    public void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (_modal.HandleClick(mouse.Position.X, mouse.Position.Y)) return;
    
        _inputHandler.ResetTypingState();
    
        if (button == MouseButton.Left)
        {
            var pos = mouse.Position;
            float gutterWidth = _editor.GetGutterWidth();

            if (pos.X < _editor.Bounds.Left + gutterWidth)
            {
                _editor.HandleGutterClick(pos.X, pos.Y);
                return;
            }

            if (_editor.Bounds.Contains(pos.X, pos.Y))
            {
                _isMouseDown = true;
                var (line, col) = _editor.GetTextPositionFromMouse(pos.X, pos.Y);
            
                _cursor.ClearSelection();
                _cursor.SetPosition(line, col);
                _cursor.StartSelection();
            }
        }
    }

    public void OnMouseMove(IMouse mouse, System.Numerics.Vector2 position)
    {
        if (_modal.IsVisible) return;
        
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
        if (_modal.IsVisible)
        {
            _isMouseDown = false;
            return;
        }
        
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
        if (_modal.IsVisible) return;
        
        var keyboard = _inputContext.Keyboards[0];
    
        bool ctrl = keyboard.IsKeyPressed(Key.ControlLeft) || keyboard.IsKeyPressed(Key.ControlRight);
        bool shift = keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight);

        _inputHandler.HandleMouseScroll(scroll.Y, ctrl, shift);
    }
}