using System.Numerics;
using System.Runtime.InteropServices;

namespace Solas.Render.Components;

[StructLayout(LayoutKind.Sequential, Size = 16)]
internal struct ObjectDataGpu
{
    internal Vector4 WorldCenterAndRadius;
}