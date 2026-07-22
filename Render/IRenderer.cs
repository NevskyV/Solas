using Silk.NET.Maths;
using Silk.NET.Windowing;
using Solas.Render.Data;
using Solas.Transform;

namespace Solas.Render;

internal interface IRenderer : IDisposable
{
    internal void Start(IWindow window, TransformData cameraTransform, CameraData cameraData);
    internal void DrawFrame();
    internal void OnResize(Vector2D<int> newSize);
}