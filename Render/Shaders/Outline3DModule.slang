module Outline3DModule;

import MaterialInterfaces;
import Material3D;

public struct Outline3DParams
{
    public float4 outlineColor;
    public float thickness;
    private float3 pad;
};

[Domain3D]
public struct Outline3DModifier : IFragmentModifier
{
    public Outline3DParams params;

    public float4 ModifyColor(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal)
    {
        float thickness = params.thickness > 0.0001f ? params.thickness : 0.3f;
        float4 color = params.outlineColor.a > 0.0001f ? params.outlineColor : float4(1.0f, 0.0f, 0.0f, 1.0f);

        float3 N = normalize(worldNormal);
        float3 viewPos = mul(ubo.view, float4(worldPos, 1.0f)).xyz;
        float3 V = normalize(mul(transpose((float3x3)ubo.view), -viewPos));

        float NdotV = saturate(dot(N, V));
        float edgeWidth = clamp(thickness * 0.35f, 0.02f, 0.6f);
        float edge = smoothstep(edgeWidth, 0.0f, NdotV);

        if (edge > 0.001f)
        {
            float3 finalRgb = lerp(inColor.rgb, color.rgb, edge * color.a);
            return float4(finalRgb, inColor.a);
        }

        return inColor;
    }
};