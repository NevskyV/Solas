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
        float3 baseAlbedo = inColor.rgb;

        float3 accumulatedLight = float3(0.05f) * params.ambientIntensity;

        for (uint i = 0u; i < ubo.directionalLightCount; ++i)
        {
            Light dirLight = g_Lights[i];
            float3 L = normalize(-dirLight.GetDirection());
            float NdotL = max(dot(N, L), 0.0f);
            accumulatedLight += dirLight.GetColor() * dirLight.GetIntensity() * NdotL;
        }

        uint3 tileCount = uint3((uint)ubo.tileCount.x, (uint)ubo.tileCount.y, (uint)ubo.tileCount.z);
        float nClip = max(ubo.nearClip, 0.001f);
        float fClip = max(ubo.farClip, nClip + 0.001f);

        float4 viewPos4 = mul(ubo.view, float4(worldPos, 1.0f));
        float depth = -viewPos4.z;
        float4 clipPos = mul(ubo.proj, viewPos4);
        float invW = 1.0f / clipPos.w;
        float screenU = saturate(clipPos.x * invW * 0.5f + 0.5f);
        float screenV = saturate(-clipPos.y * invW * 0.5f + 0.5f);

        uint tileX = min(uint(screenU * float(tileCount.x)), tileCount.x - 1u);
        uint tileY = min(uint(screenV * float(tileCount.y)), tileCount.y - 1u);
        uint sliceZ = 0u;
        if (tileCount.z > 1u)
        {
            float depthClamped = clamp(depth, nClip, fClip);
            float factor = log(depthClamped / nClip) / log(fClip / nClip);
            sliceZ = min(uint(factor * float(tileCount.z)), tileCount.z - 1u);
        }

        uint clusterIndex = tileX + tileY * tileCount.x + sliceZ * tileCount.x * tileCount.y;
        TileGrid tile = g_TileGrid[clusterIndex];

        uint lightCount = tile.lightCount;
        for (uint l = 0u; l < lightCount; ++l)
        {
            uint lightIndex = g_GlobalLightIndices[tile.lightOffset + l];
            Light pointLight = g_Lights[lightIndex];

            float3 lightPos = pointLight.GetPosition();
            float radius = pointLight.GetRadius();

            float3 lightDir = lightPos - worldPos;
            float dist = length(lightDir);

            if (dist < radius && dist > 0.001f)
            {
                float3 L = lightDir / dist;
                float NdotL = max(dot(N, L), 0.0f);
                float atten = saturate(1.0f - (dist / radius) * (dist / radius));
                accumulatedLight += pointLight.GetColor() * pointLight.GetIntensity() * NdotL * atten;
            }
        }

        return float4(baseAlbedo * accumulatedLight, inColor.a);
    }
};