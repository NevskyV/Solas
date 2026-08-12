using System.Numerics;
using System.Runtime.InteropServices;

namespace Solas.Render.Components;

[StructLayout(LayoutKind.Sequential, Size = 80)]
internal record struct LightGpu(
    Vector4 PositionOrDirection,
    Vector4 ColorIntensity,
    Vector4 ShadowParams,
    Vector4 Extra0 = default,
    Vector4 Extra1 = default
)
{
    internal static LightGpu CreatePoint(Vector3 position, float radius, Vector3 color, float intensity,
        bool castShadows, float shadowBias, float shadowSoftness, float shadowStrength)
    {
        return new LightGpu(
            PositionOrDirection: new Vector4(position, 0.0f),
            ColorIntensity: new Vector4(color, intensity),
            ShadowParams: new Vector4(castShadows ? 0.0f : -1.0f, shadowBias, shadowSoftness, shadowStrength),
            Extra0: new Vector4(radius, 0.0f, 0.0f, 0.0f)
        );
    }

    internal static LightGpu CreateSpot(Vector3 position, Quaternion rotation, float radius, float innerAngleDeg,
        float outerAngleDeg, Vector3 color, float intensity, bool castShadows, float shadowBias, float shadowSoftness,
        float shadowStrength)
    {
        var innerRad = innerAngleDeg * MathF.PI / 180.0f;
        var outerRad = outerAngleDeg * MathF.PI / 180.0f;
        var dirNorm = Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, rotation));

        return new LightGpu(
            PositionOrDirection: new Vector4(position, 1.0f),
            ColorIntensity: new Vector4(color, intensity),
            ShadowParams: new Vector4(castShadows ? 0.0f : -1.0f, shadowBias, shadowSoftness, shadowStrength),
            Extra0: new Vector4(dirNorm, radius),
            Extra1: new Vector4(MathF.Cos(innerRad * 0.5f), MathF.Cos(outerRad * 0.5f), 0.0f, 0.0f)
        );
    }

    internal static LightGpu CreateDirectional(Quaternion rotation, Vector3 color, float intensity,
        bool castShadows, float shadowBias, float shadowSoftness, float shadowStrength)
    {
        return new LightGpu(
            PositionOrDirection: new Vector4(Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, rotation)), 2.0f),
            ColorIntensity: new Vector4(color, intensity),
            ShadowParams: new Vector4(castShadows ? 0.0f : -1.0f, shadowBias, shadowSoftness, shadowStrength)
        );
    }
}