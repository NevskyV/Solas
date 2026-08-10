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
        float luma = dot(color, float3(0.2126f, 0.7152f, 0.0722f));
        float soft = clamp(luma - thresh + knee, 0.0f, 2.0f * knee);
        soft = (soft * soft) / (4.0f * knee + 0.00001f);
        float weight = max(luma - thresh, soft) / max(luma, 0.00001f);
        return color * max(weight, 0.0f);
    }

    public float4 ModifyColor(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal)
    {
        float thresh = params.threshold > 0.0001f ? params.threshold : 0.5f;
        float knee = max(params.knee, 0.05f);
        float intensity = params.bloomIntensity > 0.0001f ? params.bloomIntensity : 1.0f;
        float radius = params.blurRadius > 0.0001f ? params.blurRadius : 2.5f;

        float2 texel = float2(1.0f / 1920.0f, 1.0f / 1080.0f) * radius;

        float3 A = ExtractBright(texture.Sample(uv + float2(-2.0f, 2.0f) * texel).rgb, thresh, knee);
        float3 B = ExtractBright(texture.Sample(uv + float2(0.0f, 2.0f) * texel).rgb, thresh, knee);
        float3 C = ExtractBright(texture.Sample(uv + float2(2.0f, 2.0f) * texel).rgb, thresh, knee);
        float3 D = ExtractBright(texture.Sample(uv + float2(-2.0f, 0.0f) * texel).rgb, thresh, knee);
        float3 E = ExtractBright(texture.Sample(uv).rgb, thresh, knee);
        float3 F = ExtractBright(texture.Sample(uv + float2(2.0f, 0.0f) * texel).rgb, thresh, knee);
        float3 G = ExtractBright(texture.Sample(uv + float2(-2.0f, -2.0f) * texel).rgb, thresh, knee);
        float3 H = ExtractBright(texture.Sample(uv + float2(0.0f, -2.0f) * texel).rgb, thresh, knee);
        float3 I = ExtractBright(texture.Sample(uv + float2(2.0f, -2.0f) * texel).rgb, thresh, knee);

        float3 J = ExtractBright(texture.Sample(uv + float2(-1.0f, 1.0f) * texel).rgb, thresh, knee);
        float3 K = ExtractBright(texture.Sample(uv + float2(1.0f, 1.0f) * texel).rgb, thresh, knee);
        float3 L = ExtractBright(texture.Sample(uv + float2(-1.0f, -1.0f) * texel).rgb, thresh, knee);
        float3 M = ExtractBright(texture.Sample(uv + float2(1.0f, -1.0f) * texel).rgb, thresh, knee);

        float3 sampleSum = (A + C + G + I) * 0.03125f;
        sampleSum += (B + D + F + H) * 0.0625f;
        sampleSum += E * 0.125f;
        sampleSum += (J + K + L + M) * 0.125f;

        return float4(sampleSum * intensity, inColor.a);
    }
};