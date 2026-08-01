using System.Numerics;
using System.Runtime.InteropServices;

namespace Solas.Render.Vulkan.Components;

[StructLayout(LayoutKind.Sequential, Size = 160)]
public struct FrameParamsGpu
{
    public Matrix4x4 ViewMatrix;
    public Matrix4x4 InvProjectionMatrix;
    public Vector2 ScreenResolution;
    public Vector2 TileCount;
    public uint TotalLightCount;
    private float _pad0;
    private float _pad1;
    private float _pad2;
}