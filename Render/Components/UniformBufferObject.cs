using System.Numerics;
using System.Runtime.InteropServices;

namespace Solas.Render.Components;

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct UniformBufferObject
{
    public Matrix4x4 Model;
    public Matrix4x4 View;
    public Matrix4x4 Proj;

    public Vector3 CamPos;
    private float _pad0;

    public Vector4 TileCount;

    public float TileSize;
    public float NearClip;
    public float FarClip;
    public uint IsOrthographic;

    public uint TotalLightCount;
    public uint DirectionalLightCount;
    private ulong _pad1;
}