using System.Numerics;
using System.Runtime.InteropServices;

namespace Solas.Render.Components;

[StructLayout(LayoutKind.Sequential)]
public struct UniformBufferObject
{
    public Matrix4x4 Model;
    public Matrix4x4 View;
    public Matrix4x4 Proj;
    public Vector2 TileCount;
    public float TileSize;
    private float _pad;
}