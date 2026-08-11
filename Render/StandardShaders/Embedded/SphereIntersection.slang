module SphereIntersection;

public bool SphereVsAABB(float3 sphereCenter, float radius, float3 boxMin, float3 boxMax)
{
    float3 closestPoint = clamp(sphereCenter, boxMin, boxMax);
    float3 d = sphereCenter - closestPoint;
    return dot(d, d) <= (radius * radius);
}