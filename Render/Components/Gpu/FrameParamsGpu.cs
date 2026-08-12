using System.Numerics;
using System.Runtime.InteropServices;

namespace Solas.Render.Components;

[StructLayout(LayoutKind.Sequential, Size = 192)]
internal struct FrameParamsGpu
{
    internal Matrix4x4 LightViewProj;
    internal Vector4 CameraPosition;
    internal Vector4 CameraRight;
    internal Vector4 CameraUp;
    internal Vector4 CameraForward;
    internal Vector4 ScreenResolution;
    internal Vector4 TileCount;
    internal uint TotalLightCount;
    internal uint DirectionalLightCount;
    internal float NearClip;
    internal float FarClip;
    internal uint IsOrthographic;
    internal float TanHalfFovX;
    internal float TanHalfFovY;
    private float _pad;
}