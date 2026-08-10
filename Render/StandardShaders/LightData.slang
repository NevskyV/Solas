module LightData;

public struct Light
{
    public float4 positionOrDirection;
    public float4 colorIntensity;
    public float4 shadowParams;
    public float4 extra0;
    public float4 extra1;

    public uint GetLightType()
    {
        return (uint)positionOrDirection.w;
    }

    public float3 GetPosition()
    {
        return positionOrDirection.xyz;
    }

    public float3 GetDirection()
    {
        if (GetLightType() == 2)
        {
            return positionOrDirection.xyz;
        }
        return extra0.xyz;
    }

    public float GetRadius()
    {
        if (GetLightType() == 1)
        {
            return extra0.w;
        }
        return extra0.x;
    }

    public float3 GetColor()
    {
        return colorIntensity.xyz;
    }

    public float GetIntensity()
    {
        return colorIntensity.w;
    }

    public int GetShadowMapIndex()
    {
        return (int)shadowParams.x;
    }

    public float GetShadowBias()
    {
        return shadowParams.y;
    }

    public float GetShadowSoftness()
    {
        return shadowParams.z;
    }

    public float GetShadowStrength()
    {
        return shadowParams.w;
    }

    public bool CastsShadows()
    {
        return shadowParams.x >= 0.0f;
    }
};