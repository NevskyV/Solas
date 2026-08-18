using System.Numerics;
using System.Runtime.InteropServices;

namespace Solas.Render.Components;

[StructLayout(LayoutKind.Sequential, Size = 96)]
internal struct ShadowMatrixGpu
{
    internal Matrix4x4 LightViewProj;
    internal Vector4 LightPosAndRadius;
    internal Vector4 ShadowParams;
}