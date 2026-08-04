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

    public float3 CalculatePBR(float3 worldPos, float3 N, float3 V, float3 albedo, float roughness, float metallic, float3 F0)
    {
        uint type = GetLightType();
        float3 lightColor = colorIntensity.xyz * colorIntensity.w;
        float3 L;
        float attenuation = 1.0f;

        if (type == 2)
        {
            L = normalize(-positionOrDirection.xyz);
        }
        else
        {
            float3 lightDir = positionOrDirection.xyz - worldPos;
            float distance = length(lightDir);
            float radius = GetRadius();

            if (distance > radius)
            {
                return float3(0.0f);
            }

            L = lightDir / max(distance, 0.0001f);

            attenuation = saturate(1.0f - (distance / radius));
            attenuation *= attenuation;

            if (type == 1)
            {
                float innerAngleCos = extra1.x;
                float outerAngleCos = extra1.y;
                float theta = dot(-L, normalize(extra0.xyz));
                float epsilon = innerAngleCos - outerAngleCos;
                attenuation *= saturate((theta - outerAngleCos) / max(epsilon, 0.0001f));
            }
        }

        float NdotL = max(dot(N, L), 0.0f);
        if (NdotL <= 0.0f)
        {
            return float3(0.0f);
        }

        float NdotV = max(dot(N, V), 0.001f);
        float3 H = normalize(L + V);
        float NdotH = max(dot(N, H), 0.0f);
        float VdotH = max(dot(V, H), 0.0f);

        float a = roughness * roughness;
        float a2 = a * a;
        float denomD = NdotH * NdotH * (a2 - 1.0f) + 1.0f;
        float D = a2 / (3.14159265f * denomD * denomD + 0.00001f);

        float k = ((roughness + 1.0f) * (roughness + 1.0f)) / 8.0f;
        float gV = NdotV / (NdotV * (1.0f - k) + k);
        float gL = NdotL / (NdotL * (1.0f - k) + k);
        float G = gV * gL;

        float3 F = F0 + (1.0f - F0) * pow(saturate(1.0f - VdotH), 5.0f);

        float3 specular = (D * G * F) / max(4.0f * NdotV * NdotL, 0.001f);

        float3 kD = (float3(1.0f) - F) * (1.0f - metallic);
        float3 diffuse = kD * albedo / 3.14159265f;

        return (diffuse + specular) * lightColor * NdotL * attenuation;
    }
};