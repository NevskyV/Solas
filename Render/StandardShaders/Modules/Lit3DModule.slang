module Lit3DModule;

import MaterialInterfaces;
import Lighting;
import LightData;
import Material3D;

[[vk::binding(2, 1)]] public Sampler2D normalMap;
[[vk::binding(3, 1)]] public Sampler2D roughnessTexture;
[[vk::binding(4, 1)]] public Sampler2D metallicTexture;
[[vk::binding(5, 1)]] public Sampler2D ambientTexture;

public struct Lit3DParams
{
    public float roughness;
    public float metallic;
    public float ambientIntensity;
    public float normalMapIntensity;
    public uint hasAlbedoTexture;
    public uint hasNormalMap;
    public uint hasRoughnessTexture;
    public uint hasMetallicTexture;
    public uint hasAmbientTexture;
    private float2 pad;
};

[Domain3D]
public struct Lit3DModifier : IFragmentModifier
{
    public Lit3DParams params;

    public float4 ModifyColor(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal)
    {
        float3 N = normalize(worldNormal);
        if (params.hasNormalMap > 0)
        {
            float3 mapN = normalMap.Sample(uv).rgb * 2.0f - 1.0f;
            float scale = (params.normalMapIntensity > 0.0001f) ? params.normalMapIntensity : 1.0f;
            N = normalize(worldNormal + mapN * scale);
        }

        float paramRough = params.roughness > 0.001f ? params.roughness : 0.5f;
        float texRough = (params.hasRoughnessTexture > 0) ? roughnessTexture.Sample(uv).r : 1.0f;
        float roughnessVal = clamp(paramRough * texRough, 0.04f, 1.0f);

        float paramMetal = params.metallic;
        float texMetal = (params.hasMetallicTexture > 0) ? metallicTexture.Sample(uv).r : 1.0f;
        float metallicVal = saturate(paramMetal * texMetal);

        float paramAmb = params.ambientIntensity > 0.001f ? params.ambientIntensity : 0.05f;
        float texAmb = (params.hasAmbientTexture > 0) ? ambientTexture.Sample(uv).r : 1.0f;
        float ambientVal = paramAmb * texAmb;

        float3 viewPos = mul(ubo.view, float4(worldPos, 1.0f)).xyz;
        float3 viewDir = normalize(mul(transpose((float3x3)ubo.view), -viewPos));

        uint2 tileCount = uint2((uint)ubo.tileCount.x, (uint)ubo.tileCount.y);
        float3 litRgb = EvaluateForwardPlusPBRLighting(worldPos, N, viewDir, inColor.rgb, roughnessVal, metallicVal, ambientVal, tileCount, ubo.tileSize, ubo.view, ubo.proj);

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