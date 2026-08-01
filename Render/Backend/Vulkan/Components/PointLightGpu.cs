using System.Numerics;
using System.Runtime.InteropServices;

namespace Solas.Render.Vulkan.Components;

[StructLayout(LayoutKind.Sequential, Size = 32)]
internal struct PointLightGpu
{
    internal Vector3 Position;
    internal float Radius;
    internal Vector3 Color;
    internal float Intensity;
}