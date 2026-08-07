using System.Runtime.InteropServices;

namespace Solas.Render.Components;

[StructLayout(LayoutKind.Sequential, Size = 8)]
internal struct TileGridGpu
{
    internal uint LightOffset;
    internal uint LightCount;
}