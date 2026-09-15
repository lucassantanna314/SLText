using Silk.NET.Input;
using SkiaSharp;
using System.Collections.Generic;

namespace SLText.View.UI.Input;

/// <summary>
/// Central event dispatcher for all IInputElement components.
/// 
/// Keyboard events: first-match wins (the first active element in registration order that returns true consumes it).
/// Mouse click: last-match wins (reverse order — topmost overlay over lower panels).
/// Mouse wheel: same reverse order as click.
/// 
/// Registration order determines keyboard priority. Insertion order determines click/wheel priority (LIFO = topmost).
/// </summary>
public class InputManager
{
    private readonly List<(IInputElement Element, int RegisterOrder)> _elements = new();

    /// <summary>Register an input element. Lower RegisterOrder = higher keyboard priority.</summary>
    public void Add(IInputElement element, int registerOrder = 0)
    {
        _elements.Add((element, registerOrder));
    }

    /// <summary>Remove a previously registered element.</summary>
    public void Remove(IInputElement element) => _elements.RemoveAll(e => e.Element == element);

    /// <summary>Clear all registrations.</summary>
    public void Clear() => _elements.Clear();

    /// <summary>Get count of registered elements (for debugging).</summary>
    public int Count => _elements.Count;

    /// <summary>Keyboard down — first-match wins. Returns true if consumed.</summary>
    public bool ProcessKeyDown(Silk.NET.Input.IKeyboard k, Key key)
    {
        foreach (var (element, _) in _elements)
        {
            if (!element.IsActive || element.Bounds == SKRect.Empty) continue;
            if (element.HandleKeyDown(k, key)) return true;
        }
        return false;
    }

    /// <summary>Keyboard up — first-match wins.</summary>
    public bool ProcessKeyUp(Silk.NET.Input.IKeyboard k, Key key)
    {
        foreach (var (element, _) in _elements)
        {
            if (!element.IsActive || element.Bounds == SKRect.Empty) continue;
            if (element.HandleKeyUp(k, key)) return true;
        }
        return false;
    }

    /// <summary>Mouse click — LIFO/topmost-first. Returns true if consumed.</summary>
    public bool ProcessClick(float x, float y)
    {
        for (int i = _elements.Count - 1; i >= 0; i--)
        {
            var (element, _) = _elements[i];
            if (!element.IsActive || element.Bounds == SKRect.Empty) continue;
            if (element.Bounds.Contains(x, y) && element.HandleClick(x, y)) return true;
        }
        return false;
    }

    /// <summary>Mouse wheel — LIFO/topmost-first. Returns true if consumed.</summary>
    public bool ProcessWheel(float x, float y, float deltaY)
    {
        for (int i = _elements.Count - 1; i >= 0; i--)
        {
            var (element, _) = _elements[i];
            if (!element.IsActive || element.Bounds == SKRect.Empty) continue;
            if (element.Bounds.Contains(x, y) && element.HandleWheel(0, deltaY)) return true;
        }
        return false;
    }
}
