module Lit3DModule;

import MaterialInterfaces;
import Lighting;
import LightData;
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
    
    public float3 EvaluateForwardPlusPBRLighting(float3 worldPos, float3 normal, float3 viewDir, float3 albedo, float roughness, float metallic, float ambientIntensity, uint2 tileCount, float tileSize, float4x4 viewMat, float4x4 projMat)
    {
        float3 N = normalize(normal);
        float3 V = normalize(viewDir);
        float3 F0 = lerp(float3(0.04f), albedo, metallic);
    
        float4 clipPos = mul(projMat, mul(viewMat, float4(worldPos, 1.0f)));
        float2 ndc = clipPos.xy / max(abs(clipPos.w), 0.0001f);
        float2 uvScreen = clamp(ndc * 0.5f + 0.5f, 0.0f, 0.999f);
    
        uint2 tileID = uint2(uvScreen * float2(tileCount));
        uint tileIndex = tileID.x + tileID.y * tileCount.x;
    
        TileGrid tile = g_TileGrid[tileIndex];
        float3 Lo = float3(0.0f);
    
        uint count = min(tile.lightCount, 64u);
        for (uint i = 0; i < count; i++)
        {
            uint globalLightIndex = g_GlobalLightIndices[tile.lightOffset + i];
            Light light = g_Lights[globalLightIndex];
            Lo += light.CalculatePBR(worldPos, N, V, albedo, roughness, metallic, F0);
        }
    
        float3 ambient = albedo * ambientIntensity;
        return Lo + ambient;
    }
};