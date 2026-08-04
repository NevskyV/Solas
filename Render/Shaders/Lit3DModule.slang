module Lit3DModule;

import MaterialInterfaces;
import Lighting;
import Material3D;

public struct Lit3DParams
{
    public float roughness;
    public float metallic;
    public float ambientIntensity;
    private float pad;
};

[Domain3D]
public struct Lit3DModifier : IFragmentModifier
{
    public Lit3DParams params;

    public float4 ModifyColor(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal)
    {
        float roughnessClamped = params.roughness > 0.001f ? clamp(params.roughness, 0.04f, 1.0f) : 0.5f;
        float metallicClamped = saturate(params.metallic);
        float ambientVal = params.ambientIntensity > 0.001f ? params.ambientIntensity : 0.05f;

        float3 viewPos = mul(ubo.view, float4(worldPos, 1.0f)).xyz;
        float3 viewDir = normalize(mul(transpose((float3x3)ubo.view), -viewPos));

        uint2 tileCount = uint2((uint)ubo.tileCount.x, (uint)ubo.tileCount.y);
        float3 litRgb = EvaluateForwardPlusPBRLighting(worldPos, worldNormal, viewDir, inColor.rgb, roughnessClamped, metallicClamped, ambientVal, tileCount, ubo.tileSize, ubo.view, ubo.proj);

        return float4(litRgb, inColor.a);
    }
};