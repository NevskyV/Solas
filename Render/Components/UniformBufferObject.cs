using System.Numerics;

namespace Solas.Render.Components;

public struct UniformBufferObject
{
    public Matrix4x4 Model;
    public Matrix4x4 View;
    public Matrix4x4 Proj;
}