using System.Runtime.InteropServices;

namespace Solas.Render.Vulkan.Components;

[StructLayout(LayoutKind.Sequential, Size = 8)]
public struct TileGridGpu
{
    public uint LightOffset;
    public uint LightCount;
}