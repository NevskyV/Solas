module LightCulling;

import PointLight;
import SphereIntersection;

public struct TileGrid
{
    public uint lightOffset;
    public uint lightCount;
};

[[vk::binding(0, 0)]] StructuredBuffer<PointLight> g_Lights;
[[vk::binding(1, 0)]] RWStructuredBuffer<uint> g_GlobalLightIndices;
[[vk::binding(2, 0)]] RWStructuredBuffer<TileGrid> g_TileGrid;
[[vk::binding(3, 0)]] RWStructuredBuffer<uint> g_GlobalIndexCounter;

[[vk::binding(0, 1)]]
cbuffer FrameParams
{
    float4x4 ViewMatrix;
    float4x4 InvProjectionMatrix;
    float2 ScreenResolution;
    float2 TileCount;
    uint TotalLightCount;
};

groupshared uint s_TileLightCount;
groupshared uint s_TileLightIndexOffset;
groupshared uint s_TileLightList[256];

float3 UnprojectNDC(float2 ndc)
{
    float4 clip = float4(ndc.x, ndc.y, 1.0f, 1.0f);
    float4 view = mul(InvProjectionMatrix, clip);
    return normalize(view.xyz / view.w);
}

[shader("compute")]
[numthreads(16, 16, 1)]
void computeMain(
    uint3 threadID : SV_DispatchThreadID,
    uint3 groupID : SV_GroupID,
    uint localIndex : SV_GroupIndex)
{
    if (localIndex == 0)
    {
        s_TileLightCount = 0;
    }
    GroupMemoryBarrierWithGroupSync();

    uint2 tileID = groupID.xy;

    float2 pxMin = (float2(tileID) / TileCount) * 2.0f - 1.0f;
    float2 pxMax = (float2(tileID + uint2(1, 1)) / TileCount) * 2.0f - 1.0f;

    float3 v00 = UnprojectNDC(float2(pxMin.x, pxMin.y));
    float3 v10 = UnprojectNDC(float2(pxMax.x, pxMin.y));
    float3 v01 = UnprojectNDC(float2(pxMin.x, pxMax.y));
    float3 v11 = UnprojectNDC(float2(pxMax.x, pxMax.y));

    Plane frustumPlanes[4];
    frustumPlanes[0].normal = normalize(cross(v01, v00));
    frustumPlanes[1].normal = normalize(cross(v10, v11));
    frustumPlanes[2].normal = normalize(cross(v00, v10));
    frustumPlanes[3].normal = normalize(cross(v11, v01));

    for (uint lightIdx = localIndex; lightIdx < TotalLightCount; lightIdx += 256)
    {
        PointLight light = g_Lights[lightIdx];
        float4 viewPos = mul(ViewMatrix, float4(light.position, 1.0f));

        bool inFrustum = true;
        for (int p = 0; p < 4; p++)
        {
            if (!SphereVsPlane(viewPos.xyz, light.radius, frustumPlanes[p]))
            {
                inFrustum = false;
                break;
            }
        }

        if (inFrustum)
        {
            uint slot;
            InterlockedAdd(s_TileLightCount, 1, slot);
            if (slot < 256)
            {
                s_TileLightList[slot] = lightIdx;
            }
        }
    }
    GroupMemoryBarrierWithGroupSync();

    if (localIndex == 0)
    {
        InterlockedAdd(g_GlobalIndexCounter[0], s_TileLightCount, s_TileLightIndexOffset);
        
        uint tileFlatIndex = tileID.x + tileID.y * (uint)TileCount.x;
        g_TileGrid[tileFlatIndex].lightOffset = s_TileLightIndexOffset;
        g_TileGrid[tileFlatIndex].lightCount = s_TileLightCount;
    }
    GroupMemoryBarrierWithGroupSync();

    for (uint i = localIndex; i < s_TileLightCount; i += 256)
    {
        g_GlobalLightIndices[s_TileLightIndexOffset + i] = s_TileLightList[i];
    }
}