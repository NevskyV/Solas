using System.Numerics;
using System.Runtime.InteropServices;

namespace Solas.Render.Components;

[StructLayout(LayoutKind.Explicit)]
public struct UniformBufferObject
{
    [FieldOffset(0)] public Matrix4x4 Model;
    [FieldOffset(64)] public Matrix4x4 View;
    [FieldOffset(128)] public Matrix4x4 Proj;
}