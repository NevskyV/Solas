module BloomExtractModule;

import MaterialInterfaces;
import MaterialScreen;

public struct BloomExtractParams
{
    public float threshold;
    public float knee;
    public float bloomIntensity;
    public float blurRadius;
};

[DomainScreen]
[PassState("None", false, true)]
public struct BloomExtractModifier : IFragmentModifier
{
    public BloomExtractParams params;

    private float3 ExtractBright(float3 color, float thresh, float knee)
    {
        float luminance = dot(color, float3(0.2126f, 0.7152f, 0.0722f));
        float soft = clamp(luminance - thresh + knee, 0.0f, 2.0f * knee);
        soft = (soft * soft) / (4.0f * knee + 0.00001f);
        float weight = max(luminance - thresh, soft) / max(luminance, 0.00001f);
        return color * max(weight, 0.0f);
    }

    public float4 ModifyColor(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal)
    {
        float thresh = params.threshold > 0.0001f ? params.threshold : 0.5f;
        float knee = max(params.knee, 0.05f);
        float intensity = params.bloomIntensity > 0.0001f ? params.bloomIntensity : 1.0f;
        float radius = params.blurRadius > 0.0001f ? params.blurRadius : 2.5f;

        float2 texel = float2(1.0f / 1920.0f, 1.0f / 1080.0f) * radius;

        float3 brightSum = ExtractBright(inColor.rgb, thresh, knee) * 0.227027f;

        brightSum += ExtractBright(texture.Sample(uv + float2(texel.x * 1.384615f, 0.0f)).rgb, thresh, knee) * 0.316216f;
        brightSum += ExtractBright(texture.Sample(uv - float2(texel.x * 1.384615f, 0.0f)).rgb, thresh, knee) * 0.316216f;
        brightSum += ExtractBright(texture.Sample(uv + float2(0.0f, texel.y * 1.384615f)).rgb, thresh, knee) * 0.316216f;
        brightSum += ExtractBright(texture.Sample(uv - float2(0.0f, texel.y * 1.384615f)).rgb, thresh, knee) * 0.316216f;

        brightSum += ExtractBright(texture.Sample(uv + float2(texel.x * 2.5f, texel.y * 2.5f)).rgb, thresh, knee) * 0.070270f;
        brightSum += ExtractBright(texture.Sample(uv - float2(texel.x * 2.5f, texel.y * 2.5f)).rgb, thresh, knee) * 0.070270f;
        brightSum += ExtractBright(texture.Sample(uv + float2(-texel.x * 2.5f, texel.y * 2.5f)).rgb, thresh, knee) * 0.070270f;
        brightSum += ExtractBright(texture.Sample(uv + float2(texel.x * 2.5f, -texel.y * 2.5f)).rgb, thresh, knee) * 0.070270f;

        return float4(brightSum * intensity, inColor.a);
    }
};