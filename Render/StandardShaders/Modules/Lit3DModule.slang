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

        uint3 tileCount = uint3((uint)ubo.tileCount.x, (uint)ubo.tileCount.y, (uint)ubo.tileCount.z);
        float3 litRgb = EvaluateClusteredPBRLighting(worldPos, N, viewDir, inColor.rgb, roughnessVal, metallicVal, ambientVal, tileCount, ubo.tileSize, ubo.nearClip, ubo.farClip, ubo.isOrthographic, ubo.view, ubo.proj);

        return float4(litRgb, inColor.a);
    }
    
    public float3 EvaluateClusteredPBRLighting(float3 worldPos, float3 normal, float3 viewDir, float3 albedo, float roughness, float metallic, float ambientIntensity, uint3 tileCount, float tileSize, float nearClip, float farClip, uint isOrthographic, float4x4 viewMat, float4x4 projMat)
    {
        float3 N = normalize(normal);
        float3 V = normalize(viewDir);
        float3 F0 = lerp(float3(0.04f), albedo, metallic);
    
        float4 viewPos4 = mul(viewMat, float4(worldPos, 1.0f));
        float4 clipPos = mul(projMat, viewPos4);
        float2 ndc = clipPos.xy / max(abs(clipPos.w), 0.0001f);
        float2 uvScreen = clamp(ndc * 0.5f + 0.5f, 0.0f, 0.999f);
    
        uint2 tileID = uint2(uvScreen * float2(tileCount.xy));

        uint sliceZ = 0;
        if (tileCount.z > 1u)
        {
            float depth = -viewPos4.z;
            if (isOrthographic != 0u)
            {
                float factor = saturate((depth - nearClip) / max(farClip - nearClip, 0.001f));
                sliceZ = uint(clamp(factor * float(tileCount.z), 0.0f, float(tileCount.z - 1u)));
            }
            else
            {
                float logNear = max(nearClip, 0.001f);
                float logFar = max(farClip, logNear + 0.001f);
                float depthClamped = clamp(depth, logNear, logFar);
                float factor = log(depthClamped / logNear) / log(logFar / logNear);
                sliceZ = uint(clamp(factor * float(tileCount.z), 0.0f, float(tileCount.z - 1u)));
            }
        }

        uint clusterIndex = tileID.x + tileID.y * tileCount.x + sliceZ * tileCount.x * tileCount.y;
    
        TileGrid tile = g_TileGrid[clusterIndex];
        float3 Lo = float3(0.0f);
    
        uint count = min(tile.lightCount, 64u);
        for (uint i = 0; i < count; i++)
        {
            uint globalLightIndex = g_GlobalLightIndices[tile.lightOffset + i];
            Light light = g_Lights[globalLightIndex];
            Lo += CalculatePBR(light, worldPos, N, V, albedo, roughness, metallic, F0);
        }
    
        float3 ambient = albedo * ambientIntensity;
        return Lo + ambient;
    }
    
    public float3 CalculatePBR(Light light, float3 worldPos, float3 N, float3 V, float3 albedo, float roughness, float metallic, float3 F0)
    {
        uint type = light.GetLightType();
        float3 lightColor = light.colorIntensity.xyz * light.colorIntensity.w;
        float3 L;
        float attenuation = 1.0f;

        if (type == 2)
        {
            L = normalize(-light.positionOrDirection.xyz);
        }
        else
        {
            float3 lightDir = light.positionOrDirection.xyz - worldPos;
            float distance = length(lightDir);
            float radius = light.GetRadius();

            if (distance > radius)
            {
                return float3(0.0f);
            }

            L = lightDir / max(distance, 0.0001f);

            attenuation = saturate(1.0f - (distance / radius));
            attenuation *= attenuation;

            if (type == 1)
            {
                float innerAngleCos = light.extra1.x;
                float outerAngleCos = light.extra1.y;
                float theta = dot(-L, normalize(light.extra0.xyz));
                float epsilon = innerAngleCos - outerAngleCos;
                attenuation *= saturate((theta - outerAngleCos) / max(epsilon, 0.0001f));
            }
        }

        float NdotL = max(dot(N, L), 0.0f);
        if (NdotL <= 0.0f)
        {
            return float3(0.0f);
        }

        float NdotV = max(dot(N, V), 0.001f);
        float3 H = normalize(L + V);
        float NdotH = max(dot(N, H), 0.0f);
        float VdotH = max(dot(V, H), 0.0f);

        float a = roughness * roughness;
        float a2 = a * a;
        float denomD = NdotH * NdotH * (a2 - 1.0f) + 1.0f;
        float D = a2 / (3.14159265f * denomD * denomD + 0.00001f);

        float k = ((roughness + 1.0f) * (roughness + 1.0f)) / 8.0f;
        float gV = NdotV / (NdotV * (1.0f - k) + k);
        float gL = NdotL / (NdotL * (1.0f - k) + k);
        float G = gV * gL;

        float3 F = F0 + (1.0f - F0) * pow(saturate(1.0f - VdotH), 5.0f);

        float3 specular = (D * G * F) / max(4.0f * NdotV * NdotL, 0.001f);

        float3 kD = (float3(1.0f) - F) * (1.0f - metallic);
        float3 diffuse = kD * albedo / 3.14159265f;

        return (diffuse + specular) * lightColor * NdotL * attenuation;
    }
};