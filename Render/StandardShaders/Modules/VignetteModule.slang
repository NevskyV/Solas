module VignetteModule;

import MaterialInterfaces;

public struct VignetteParams
{
    public float power;
    public float scale;
    private float2 pad;
};

[DomainScreen]
public struct VignetteModifier : IFragmentModifier
{
    public VignetteParams params;

    public float4 ModifyColor(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal)
    {
        float p = params.power > 0.0001f ? params.power : 1.5f;
        float s = params.scale > 0.0001f ? params.scale : 0.15f;

        float2 dist = uv - float2(0.5f);
        float d = length(dist);
        float vignette = smoothstep(0.8f, s, d);
        vignette = pow(vignette, p);

        return float4(inColor.rgb * vignette, inColor.a);
    }
};
