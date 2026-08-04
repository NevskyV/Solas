module BaseTextureModule;

import MaterialInterfaces;

public struct BaseTextureParams
{
    public float4 tintColor;
    public float2 mainOffset;
    public float2 mainTiling;
};

[DomainUniversal]
public struct BaseTextureModifier : IFragmentModifier
{
    public BaseTextureParams params;

    public float4 ModifyColor(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal)
    {
        float2 adjustedUv = uv * params.mainTiling + params.mainOffset;
        return inColor * params.tintColor;
    }
};
