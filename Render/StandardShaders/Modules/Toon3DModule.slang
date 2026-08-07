module Toon3DModule;

import MaterialInterfaces;
import Material3D;
import Lighting;
import LightData;

public struct Toon3DParams
{
    public float steps;
    public float minBrightness;
    public float smoothness;
    public float enableSpecular;
};

[Domain3D]
public struct Toon3DModifier : IFragmentModifier
{
    public Toon3DParams params;

    private float3 EvaluateForwardPlusToonLighting(
        float3 worldPos,
        float3 worldNormal,
        float3 viewDir,
        float3 baseColor,
        float stepsCount,
        float minBright,
        float smoothness,
        float enableSpecular)
    {
        float3 N = normalize(worldNormal);
        float3 V = normalize(viewDir);

        float4 clipPos = mul(ubo.proj, mul(ubo.view, float4(worldPos, 1.0f)));
        float2 ndc = clipPos.xy / clipPos.w;
        float2 screenUV = ndc * 0.5f + float2(0.5f);
        screenUV.y = 1.0f - screenUV.y;

        uint2 tileCount = uint2((uint)ubo.tileCount.x, (uint)ubo.tileCount.y);
        uint tileSizeVal = (uint)ubo.tileSize;
        uint2 tileSize = uint2(tileSizeVal, tileSizeVal);

        float2 screenSize = float2(tileCount * tileSize);
        uint2 tileCoord = uint2(clamp(screenUV * screenSize, float2(0.0f), screenSize - 1.0f)) / tileSize;
        tileCoord = min(tileCoord, tileCount - 1);

        uint flatTileIdx = tileCoord.y * tileCount.x + tileCoord.x;
        TileGrid tile = g_TileGrid[flatTileIdx];

        float3 totalDiffuse = float3(0.0f);
        float3 totalSpecular = float3(0.0f);
        float edgeWidth = max(smoothness, 0.001f);

        for (uint i = 0u; i < tile.lightCount; ++i)
        {
            uint lightIdx = g_GlobalLightIndices[tile.lightOffset + i];
            Light light = g_Lights[lightIdx];

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
                float3 lightDir = light.GetPosition() - worldPos;
                float dist = length(lightDir);
                float radius = light.GetRadius();

                if (dist > radius)
                {
                    continue;
                }

                L = lightDir / max(dist, 0.0001f);

                attenuation = saturate(1.0f - (dist / radius));
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

            float NdotL = saturate(dot(N, L));
            float rawStep = NdotL * stepsCount;
            float stepIdx = floor(rawStep);
            float stepFrac = frac(rawStep);
            float smoothFactor = smoothstep(0.5f - edgeWidth, 0.5f + edgeWidth, stepFrac);
            float toonNdotL = (stepIdx + smoothFactor) / stepsCount;

            totalDiffuse += baseColor * lightColor * toonNdotL * attenuation;

            if (enableSpecular > 0.5f)
            {
                float3 H = normalize(L + V);
                float NdotH = saturate(dot(N, H));
                float specRaw = pow(NdotH, 32.0f);
                float specToon = smoothstep(0.5f - edgeWidth, 0.5f + edgeWidth, specRaw);
                totalSpecular += lightColor * specToon * attenuation;
            }
        }

        totalDiffuse = max(totalDiffuse, baseColor * minBright);

        return totalDiffuse + totalSpecular;
    }

    public float4 ModifyColor(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal)
    {
        float stepsCount = params.steps >= 1.0f ? params.steps : 3.0f;
        float minBright = params.minBrightness >= 0.01f ? params.minBrightness : 0.25f;
        float smoothness = clamp(params.smoothness, 0.0f, 0.5f);
        float enableSpecular = params.enableSpecular > 0.5f ? 1.0f : 0.0f;

        float3 viewPos = mul(ubo.view, float4(worldPos, 1.0f)).xyz;
        float3 viewDir = normalize(mul(transpose((float3x3)ubo.view), -viewPos));

        float3 toonColor = EvaluateForwardPlusToonLighting(
            worldPos,
            worldNormal,
            viewDir,
            inColor.rgb,
            stepsCount,
            minBright,
            smoothness,
            enableSpecular
        );

        return float4(toonColor, inColor.a);
    }
};