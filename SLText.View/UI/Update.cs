using Silk.NET.Input;

namespace SLText.View.UI;

public partial class WindowManager
{
     private void OnUpdate(double dt)
    {
        _editor.Update(dt);
        if (_terminal.IsVisible) _terminal.Update(dt);
        
        if (_lastPressedKey.HasValue && _activeKeyboard != null)
        {
            _repeatTimer += dt;
            double threshold = _isFirstRepeat ? _initialDelay : _repeatInterval;

            if (_repeatTimer >= threshold)
            {
                _repeatTimer = 0;
                _isFirstRepeat = false;
            
                ProcessKeyPress(_lastPressedKey.Value);
            }
        }

        if (_primaryMouse == null) return;
        var pos = _primaryMouse.Position;
        float mx = pos.X;
        float my = pos.Y;

        StandardCursor targetCursor = StandardCursor.Default;

        float explorerWidth = _explorer.IsVisible ? _explorer.Width : 0;

        bool isOverTerminalSplitter = _terminal.IsVisible &&
                                      Math.Abs(my - _terminal.Bounds.Top) < 15 &&
                                      mx > explorerWidth;

        bool isOverExplorerSplitter = _explorer.IsVisible &&
                                      _explorer.IsOnResizeBorder(mx) &&
                                      (!_terminal.IsVisible || my < _terminal.Bounds.Top);

        if (_terminal.IsResizing || isOverTerminalSplitter)
        {
            targetCursor = StandardCursor.VResize;
        }
        else if (_isResizingExplorer || isOverExplorerSplitter)
        {
            targetCursor = StandardCursor.HResize;
        }

        else if (_explorer.IsVisible && _explorer.Bounds.Contains(mx, my))
        {
            targetCursor = StandardCursor.Default;
        }

        if (targetCursor != _lastAppliedCursor)
        {
            _primaryMouse.Cursor.StandardCursor = targetCursor;
            _lastAppliedCursor = targetCursor;
        }

        _primaryMouse.Cursor.StandardCursor = targetCursor;
        _lastAppliedCursor = targetCursor;

        if (_pendingAction != null)
        {
            var action = _pendingAction;
            _pendingAction = null;
            action();
        }
    }
}