module ILight;

public interface ILight
{
    float3 GetPosition();
    float GetRadius();
    float3 CalculateLighting(float3 worldPos, float3 normal);
};