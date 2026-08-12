module LightCulling;

import LightData;

public struct TileGrid
{
    public uint lightOffset;
    public uint lightCount;
};

[[vk::binding(0, 0)]] StructuredBuffer<Light> g_Lights;
[[vk::binding(1, 0)]] RWStructuredBuffer<uint> g_GlobalLightIndices;
[[vk::binding(2, 0)]] RWStructuredBuffer<TileGrid> g_TileGrid;
[[vk::binding(3, 0)]] RWStructuredBuffer<uint> g_GlobalIndexCounter;

struct FrameParamsData
{
    float4x4 LightViewProj;
    float4 CameraPosition;
    float4 CameraRight;
    float4 CameraUp;
    float4 CameraForward;
    float4 ScreenResolution;
    float4 TileCount;
    uint TotalLightCount;
    uint DirectionalLightCount;
    float NearClip;
    float FarClip;
    uint IsOrthographic;
    float TanHalfFovX;
    float TanHalfFovY;
    float _pad;
};

[[vk::binding(0, 1)]] ConstantBuffer<FrameParamsData> FrameParams;

[shader("compute")]
[numthreads(1, 1, 1)]
void computeMain(uint3 groupID : SV_GroupID)
{
    uint3 clusterID = groupID.xyz;
    uint tileCountX = (uint)FrameParams.TileCount.x;
    uint tileCountY = (uint)FrameParams.TileCount.y;
    uint tileCountZ = (uint)FrameParams.TileCount.z;

    uint clusterFlatIndex = clusterID.x + clusterID.y * tileCountX + clusterID.z * tileCountX * tileCountY;

    if (FrameParams.TotalLightCount == 0u)
    {
        g_TileGrid[clusterFlatIndex].lightOffset = 0u;
        g_TileGrid[clusterFlatIndex].lightCount = 0u;
        return;
    }

    float nClip = max(FrameParams.NearClip, 0.001f);
    float fClip = max(FrameParams.FarClip, nClip + 0.001f);

    float clusterMinZ;
    float clusterMaxZ;

    if (tileCountZ <= 1u)
    {
        clusterMinZ = nClip;
        clusterMaxZ = fClip;
    }
    else
    {
        float sliceNear = float(clusterID.z) / float(tileCountZ);
        float sliceFar  = float(clusterID.z + 1u) / float(tileCountZ);

        if (FrameParams.IsOrthographic != 0u)
        {
            clusterMinZ = nClip + sliceNear * (fClip - nClip);
            clusterMaxZ = nClip + sliceFar  * (fClip - nClip);
        }
        else
        {
            float ratio = fClip / nClip;
            clusterMinZ = nClip * pow(ratio, sliceNear);
            clusterMaxZ = nClip * pow(ratio, sliceFar);
        }
    }

    float minX_NDC = (float(clusterID.x) / float(tileCountX)) * 2.0f - 1.0f;
    float maxX_NDC = (float(clusterID.x + 1u) / float(tileCountX)) * 2.0f - 1.0f;

    float minY_NDC = (float(clusterID.y) / float(tileCountY)) * 2.0f - 1.0f;
    float maxY_NDC = (float(clusterID.y + 1u) / float(tileCountY)) * 2.0f - 1.0f;

    uint localLightIndices[128];
    uint localCount = 0u;

    for (uint lightIndex = FrameParams.DirectionalLightCount; lightIndex < FrameParams.TotalLightCount; ++lightIndex)
    {
        if (localCount >= 128u)
            break;

        Light light = g_Lights[lightIndex];
        float3 lightWorldPos = light.GetPosition();
        float radius = light.GetRadius();

        float3 dir = lightWorldPos - FrameParams.CameraPosition.xyz;
        float viewX = dot(dir, FrameParams.CameraRight.xyz);
        float viewY = dot(dir, FrameParams.CameraUp.xyz);
        float viewZ = dot(dir, FrameParams.CameraForward.xyz);

        if (viewZ + radius < clusterMinZ || viewZ - radius > clusterMaxZ)
            continue;

        float z = clamp(viewZ, clusterMinZ, clusterMaxZ);

        float tileMinX = minX_NDC * z * FrameParams.TanHalfFovX;
        float tileMaxX = maxX_NDC * z * FrameParams.TanHalfFovX;

        float tileMinY = minY_NDC * z * FrameParams.TanHalfFovY;
        float tileMaxY = maxY_NDC * z * FrameParams.TanHalfFovY;

        float closestX = clamp(viewX, min(tileMinX, tileMaxX), max(tileMinX, tileMaxX));
        float closestY = clamp(viewY, min(tileMinY, tileMaxY), max(tileMinY, tileMaxY));

        float dx = viewX - closestX;
        float dy = viewY - closestY;
        float dz = viewZ - z;

        float distSq = dx * dx + dy * dy + dz * dz;

        if (distSq <= (radius * radius))
        {
            localLightIndices[localCount] = lightIndex;
            localCount++;
        }
    }

    if (localCount > 0u)
    {
        uint baseOffset = 0u;
        InterlockedAdd(g_GlobalIndexCounter[0], localCount, baseOffset);

        for (uint k = 0u; k < localCount; ++k)
        {
            g_GlobalLightIndices[baseOffset + k] = localLightIndices[k];
        }

        g_TileGrid[clusterFlatIndex].lightOffset = baseOffset;
        g_TileGrid[clusterFlatIndex].lightCount = localCount;
    }
    else
    {
        g_TileGrid[clusterFlatIndex].lightOffset = 0u;
        g_TileGrid[clusterFlatIndex].lightCount = 0u;
    }
}