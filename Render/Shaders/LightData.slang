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

    public float3 CalculateLighting(float3 worldPos, float3 normal)
    {
        uint type = GetLightType();

        if (type == 2)
        {
            float3 L = normalize(-positionOrDirection.xyz);
            float NdotL = max(dot(normal, L), 0.0f);
            return colorIntensity.xyz * colorIntensity.w * NdotL;
        }

        float3 lightDir = positionOrDirection.xyz - worldPos;
        float distance = length(lightDir);
        float radius = GetRadius();

        if (distance > radius)
        {
            return float3(0.0f);
        }

        lightDir /= distance;

        float NdotL = max(dot(normal, lightDir), 0.0f);
        if (NdotL <= 0.0f)
        {
            return float3(0.0f);
        }

        float attenuation = saturate(1.0f - (distance / radius));
        attenuation *= attenuation;

        float spotAttenuation = 1.0f;
        if (type == 1)
        {
            float innerAngleCos = extra1.x;
            float outerAngleCos = extra1.y;
            float theta = dot(-lightDir, normalize(extra0.xyz));
            float epsilon = innerAngleCos - outerAngleCos;
            spotAttenuation = saturate((theta - outerAngleCos) / max(epsilon, 0.0001f));
        }

        return colorIntensity.xyz * colorIntensity.w * NdotL * attenuation * spotAttenuation;
    }
};