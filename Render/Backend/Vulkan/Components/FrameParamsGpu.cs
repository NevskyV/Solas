using System.Numerics;
using System.Runtime.InteropServices;

namespace Solas.Render.Vulkan.Components;

[StructLayout(LayoutKind.Sequential, Size = 160)]
internal struct FrameParamsGpu
{
    internal Matrix4x4 ViewMatrix;
    internal Matrix4x4 InvProjectionMatrix;
    internal Vector2 ScreenResolution;
    internal Vector2 TileCount;
    internal uint TotalLightCount;
    private float _pad0;
    private float _pad1;
    private float _pad2;
}