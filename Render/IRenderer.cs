using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Solas.Render;

internal interface IRenderer : IDisposable
{
    internal void Start(IWindow window);
    internal void DrawFrame();
    internal void OnResize(Vector2D<int> newSize);
}