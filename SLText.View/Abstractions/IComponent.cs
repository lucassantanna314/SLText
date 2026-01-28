using SkiaSharp;

namespace SLText.View.Abstractions;

public interface IComponent
{
    SKRect Bounds { get; set; }
    void Render(SKCanvas canvas);
    void Update(double deltaTime);
}