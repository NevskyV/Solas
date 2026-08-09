module GrayScaleModule;

import MaterialInterfaces;

public struct GrayScaleParams
{
    public float intensity;
    private float3 pad;
};

[DomainScreen]
public struct GrayScaleModifier : IFragmentModifier
{
    public GrayScaleParams params;

    public float4 ModifyColor(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal)
    {
        float gray = dot(inColor.rgb, float3(0.299f, 0.587f, 0.114f));
        float factor = params.intensity > 0.0001f ? params.intensity : 1.0f;
        float3 finalRgb = lerp(inColor.rgb, float3(gray), factor);
        return float4(finalRgb, inColor.a);
    }
};
