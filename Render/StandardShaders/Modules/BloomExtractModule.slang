module BloomExtractModule;

import MaterialInterfaces;

public struct BloomExtractParams
{
    public float threshold;
    public float knee;
    private float2 pad;
};

[DomainScreen]
[PassState("None",false, true)]
public struct BloomExtractModifier : IFragmentModifier
{
    public BloomExtractParams params;

    public float4 ModifyColor(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal)
    {
        float thresh = params.threshold > 0.0001f ? params.threshold : 0.5f;
        float knee = params.knee > 0.0001f ? params.knee : 0.1f;

        float luminance = dot(inColor.rgb, float3(0.2126f, 0.7152f, 0.0722f));
        
        // Мягкая кривая обрезки (Soft Threshold)
        float rq = clamp(luminance - thresh + knee, 0.0f, 2.0f * knee);
        rq = (rq * rq) / (4.0f * knee + 0.00001f);
        float weight = max(luminance - thresh, rq) / max(luminance, 0.00001f);

        return float4(inColor.rgb * weight, inColor.a);
    }
};