using System.Numerics;
using System.Runtime.InteropServices;

namespace Solas.Render.Components;

[StructLayout(LayoutKind.Sequential, Size = 240)]
public struct UniformBufferObject
{
    public Matrix4x4 Model;
    public Matrix4x4 View;
    public Matrix4x4 Proj;
    public Vector4 TileCount;
    public float TileSize;
    public float NearClip;
    public float FarClip;
    public uint IsOrthographic;
    public uint TotalLightCount;
    public uint DirectionalLightCount;
    private Vector2 _pad;
}