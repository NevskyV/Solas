module ToonModule;

import MaterialInterfaces;
import Lighting;
import Material3D;

public struct ToonParams
{
    public float steps;
    public float minBrightness;
    private float2 pad;
};

[Domain3D]
public struct ToonModifier : IFragmentModifier
{
    public ToonParams params;

    public float4 ModifyColor(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal)
    {
        float stepsCount = params.steps >= 1.0f ? params.steps : 3.0f;
        float minBright = params.minBrightness >= 0.01f ? params.minBrightness : 0.25f;

        float3 viewPos = mul(ubo.view, float4(worldPos, 1.0f)).xyz;
        float3 viewDir = normalize(mul(transpose((float3x3)ubo.view), -viewPos));

        uint2 tileCount = uint2((uint)ubo.tileCount.x, (uint)ubo.tileCount.y);
        float3 toonColor = EvaluateForwardPlusToonLighting(worldPos, worldNormal, viewDir, inColor.rgb, stepsCount, minBright, tileCount, ubo.tileSize, ubo.view, ubo.proj);

        return float4(toonColor, inColor.a);
    }
};