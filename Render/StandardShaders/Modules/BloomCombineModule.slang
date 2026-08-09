module BloomCombineModule;

import MaterialInterfaces;

public struct BloomCombineParams
{
    public float bloomIntensity;
    private float3 pad;
};

[DomainScreen]
public struct BloomCombineModifier : IFragmentModifier
{
    public BloomCombineParams params;

    public float4 ModifyColor(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal)
    {
        float intensity = params.bloomIntensity > 0.0001f ? params.bloomIntensity : 1.2f;

        float3 finalColor = inColor.rgb * intensity;

        return float4(finalColor, inColor.a);
    }
};