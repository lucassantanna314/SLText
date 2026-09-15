using Silk.NET.Input;
using SkiaSharp;

namespace SLText.View.UI.Input;

/// <summary>
/// Declarative interface for any UI element that receives keyboard/mouse events.
/// Replaces the scattered if/else chains in WindowManager.
/// 
/// Usage: implement the interface → call InputManager.Add(this) → done.
/// WindowManager never imports specific component types again.
/// </summary>
public interface IInputElement
{
    /// <summary>Visual bounding rect in screen coordinates.</summary>
    SKRect Bounds { get; }

    /// <summary>Whether this element is currently visible and interactive.</summary>
    bool IsActive { get; }

    /// <summary>Handle key-down. Return true to consume (stop propagation).</summary>
    bool HandleKeyDown(IKeyboard keyboard, Key key);

    /// <summary>Handle key-up. Return true to consume.</summary>
    bool HandleKeyUp(IKeyboard keyboard, Key key);

    /// <summary>Handle a mouse click at (x, y). Only called if Bounds.Contains(x,y). Return true to consume.</summary>
    bool HandleClick(float x, float y);

    /// <summary>Handle mouse scroll. delta is normalized — positive = up, negative = down.</summary>
    bool HandleWheel(float deltaX, float deltaY);
}
