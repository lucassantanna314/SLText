using SLText.Core.Interfaces;

namespace SLText.Core.Commands;

public class ResetZoomCommand : ICommand
{
    private readonly IZoomable _zoomable;
    private float _previousSize;

    public ResetZoomCommand(IZoomable zoomable)
    {
        _zoomable = zoomable;
    }

    public void Execute()
    {
        _previousSize = _zoomable.FontSize;
        _zoomable.FontSize = 16f;
    }

    public void Undo() => _zoomable.FontSize = _previousSize;
}