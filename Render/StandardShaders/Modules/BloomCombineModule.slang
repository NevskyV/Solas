module BloomCombineModule;

import MaterialInterfaces;
import MaterialScreen;

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
        float intensity = params.bloomIntensity > 0.0001f ? params.bloomIntensity : 1.0f;
        float4 sceneColor = sceneTexture.Sample(uv);

        float3 finalColor = sceneColor.rgb + inColor.rgb * intensity;

        return float4(finalColor, sceneColor.a);
    }
};