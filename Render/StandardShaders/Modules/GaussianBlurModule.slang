module GaussianBlurModule;

import MaterialInterfaces;
import MaterialScreen;

public struct GaussianBlurParams
{
    public float blurRadius;
    public float2 texelSize;
    private float pad;
};

[DomainScreen]
[PassState("None",false, true)]
public struct GaussianBlurModifier : IFragmentModifier
{
    public GaussianBlurParams params;

    public float4 ModifyColor(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal)
    {
        float r = params.blurRadius > 0.0001f ? params.blurRadius : 2.0f;
        float2 delta = (params.texelSize.x > 0.00001f ? params.texelSize : float2(1.0f / 1920.0f, 1.0f / 1080.0f)) * r;

        float4 colorSum = inColor * 0.227027f;

        colorSum += texture.Sample(uv + float2(delta.x * 1.384615f, 0.0f)) * 0.316216f;
        colorSum += texture.Sample(uv - float2(delta.x * 1.384615f, 0.0f)) * 0.316216f;

        colorSum += texture.Sample(uv + float2(0.0f, delta.y * 1.384615f)) * 0.070270f;
        colorSum += texture.Sample(uv - float2(0.0f, delta.y * 1.384615f)) * 0.070270f;

        return colorSum;
    }
};