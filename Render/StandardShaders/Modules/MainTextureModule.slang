module MainTextureModule;

import MaterialInterfaces;

[[vk::binding(1, 1)]] public Sampler2D mainTexture;

public struct MainTextureParams
{
    public float4 tintColor;
};

[DomainUniversal]
public struct MainTextureModifier : IFragmentModifier
{
    public MainTextureParams params;

    public float4 ModifyColor(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal)
    {
        float4 texColor = mainTexture.Sample(uv);
        float4 tint = (length(params.tintColor) > 0.0001f) ? params.tintColor : float4(1.0f, 1.0f, 1.0f, 1.0f);
        return inColor * texColor * tint;
    }
};
