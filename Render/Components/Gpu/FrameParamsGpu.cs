using System.Numerics;
using System.Runtime.InteropServices;

namespace Solas.Render.Components;

[StructLayout(LayoutKind.Sequential, Size = 192)]
internal struct FrameParamsGpu
{
    internal Matrix4x4 ViewMatrix;
    internal Matrix4x4 InvProjectionMatrix;
    internal Vector4 ScreenResolution;
    internal Vector4 TileCount;
    internal uint TotalLightCount;
    internal float NearClip;
    internal float FarClip;
    internal uint IsOrthographic;
    private Vector4 _pad;
}