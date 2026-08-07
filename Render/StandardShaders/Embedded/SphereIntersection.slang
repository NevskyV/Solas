module SphereIntersection;

public struct Plane
{
    public float3 normal;
};

public bool SphereVsPlane(float3 center, float radius, Plane plane)
{
    return dot(plane.normal, center) >= -radius;
}