module Lighting;

import LightData;

public struct TileGrid
{
    public uint lightOffset;
    public uint lightCount;
};

[[vk::binding(0, 0)]] public StructuredBuffer<Light> g_Lights;
[[vk::binding(1, 0)]] public StructuredBuffer<uint> g_GlobalLightIndices;
[[vk::binding(2, 0)]] public StructuredBuffer<TileGrid> g_TileGrid;

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

public float3 EvaluateForwardPlusToonLighting(float3 worldPos, float3 normal, float3 viewDir, float3 albedo, float steps, float minBrightness, uint2 tileCount, float tileSize, float4x4 viewMat, float4x4 projMat)
{
    float3 N = normalize(normal);
    float3 V = normalize(viewDir);

    float stepsCount = steps >= 1.0f ? steps : 3.0f;
    float minBright = minBrightness >= 0.01f ? minBrightness : 0.25f;

    float4 clipPos = mul(projMat, mul(viewMat, float4(worldPos, 1.0f)));
    float2 ndc = clipPos.xy / max(abs(clipPos.w), 0.0001f);
    float2 uvScreen = clamp(ndc * 0.5f + 0.5f, 0.0f, 0.999f);

    uint2 tileID = uint2(uvScreen * float2(tileCount));
    uint tileIndex = tileID.x + tileID.y * tileCount.x;

    TileGrid tile = g_TileGrid[tileIndex];
    float3 totalDiffuse = float3(0.0f);
    float3 totalSpecular = float3(0.0f);

    uint count = min(tile.lightCount, 64u);
    for (uint i = 0; i < count; i++)
    {
        uint globalLightIndex = g_GlobalLightIndices[tile.lightOffset + i];
        Light light = g_Lights[globalLightIndex];

        uint type = light.GetLightType();
        float3 lightColor = light.GetColor() * light.GetIntensity();
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
                continue;
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
        float rawLight = NdotL * attenuation;

        float steppedLight = floor(rawLight * stepsCount + 0.5f) / stepsCount;
        totalDiffuse += lightColor * steppedLight;

        float3 H = normalize(L + V);
        float NdotH = max(dot(N, H), 0.0f);
        if (NdotH > 0.96f && NdotL > 0.01f)
        {
            totalSpecular += lightColor * 0.5f;
        }
    }

    float3 finalLight = max(totalDiffuse, float3(minBright));
    return albedo * finalLight + totalSpecular;
}