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

    public float4 ModifyColor(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal, float3 camPos)
    {
        float stepsCount = params.steps >= 1.0f ? params.steps : 3.0f;
        float minBright = params.minBrightness >= 0.01f ? params.minBrightness : 0.25f;
        float smoothness = clamp(params.smoothness, 0.0f, 0.5f);
        float enableSpecular = params.enableSpecular > 0.5f ? 1.0f : 0.0f;
        float edgeWidth = max(smoothness, 0.001f);

        float3 N = normalize(worldNormal);
        float3 viewPos = mul(ubo.view, float4(worldPos, 1.0f)).xyz;
        float3 V = normalize(mul(transpose((float3x3)ubo.view), -viewPos));

        float3 totalDiffuse = float3(0.0f);
        float3 totalSpecular = float3(0.0f);

        for (uint i = 0u; i < ubo.directionalLightCount; ++i)
        {
            Light dirLight = g_Lights[i];
            float3 L = normalize(-dirLight.GetDirection());
            float3 lightColor = dirLight.GetColor() * dirLight.GetIntensity();

            float NdotL = saturate(dot(N, L));
            float rawStep = NdotL * stepsCount;
            float stepIdx = floor(rawStep);
            float stepFrac = frac(rawStep);
            float smoothFactor = smoothstep(0.5f - edgeWidth, 0.5f + edgeWidth, stepFrac);
            float toonNdotL = (stepIdx + smoothFactor) / stepsCount;

            totalDiffuse += inColor.rgb * lightColor * toonNdotL;

            if (enableSpecular > 0.5f)
            {
                float3 H = normalize(L + V);
                float NdotH = saturate(dot(N, H));
                float specRaw = pow(NdotH, 32.0f);
                float specToon = smoothstep(0.5f - edgeWidth, 0.5f + edgeWidth, specRaw);
                totalSpecular += lightColor * specToon;
            }
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

            if (dist < radius && dist > 0.0001f)
            {
                float3 L = lightDir / dist;
                float attenuation = saturate(1.0f - (dist / radius) * (dist / radius));;

                float3 lightColor = pointLight.GetColor() * pointLight.GetIntensity() * attenuation;

                float NdotL = saturate(dot(N, L));
                float rawStep = NdotL * stepsCount;
                float stepIdx = floor(rawStep);
                float stepFrac = frac(rawStep);
                float smoothFactor = smoothstep(0.5f - edgeWidth, 0.5f + edgeWidth, stepFrac);
                float toonNdotL = (stepIdx + smoothFactor) / stepsCount;

                totalDiffuse += inColor.rgb * lightColor * toonNdotL;

                if (enableSpecular > 0.5f)
                {
                    float3 H = normalize(L + V);
                    float NdotH = saturate(dot(N, H));
                    float specRaw = pow(NdotH, 32.0f);
                    float specToon = smoothstep(0.5f - edgeWidth, 0.5f + edgeWidth, specRaw);
                    totalSpecular += lightColor * specToon * attenuation;
                }
            }
        }

        totalDiffuse = max(totalDiffuse, inColor.rgb * minBright);

        return float4(totalDiffuse + totalSpecular, inColor.a);
    }
};