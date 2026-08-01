module PointLight;

import ILight;

public struct PointLight : ILight
{
    public float3 position;
    public float radius;
    public float3 color;
    public float intensity;

    public float3 GetPosition()
    {
        return position;
    }

    public float GetRadius()
    {
        return radius;
    }

    public float3 CalculateLighting(float3 worldPos, float3 normal)
    {
        float3 L = position - worldPos;
        float dist = length(L);

        if (dist > radius)
        {
            return float3(0.0f, 0.0f, 0.0f);
        }

        L = normalize(L);
        float atten = 1.0f / (1.0f + (dist * dist) / (radius * radius));
        float NdotL = max(dot(normal, L), 0.0f);

        return color * intensity * NdotL * atten;
    }
};