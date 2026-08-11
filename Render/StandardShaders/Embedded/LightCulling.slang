module LightCulling;

import LightData;
import SphereIntersection;

public struct TileGrid
{
    public uint lightOffset;
    public uint lightCount;
};

[[vk::binding(0, 0)]] StructuredBuffer<Light> g_Lights;
[[vk::binding(1, 0)]] RWStructuredBuffer<uint> g_GlobalLightIndices;
[[vk::binding(2, 0)]] RWStructuredBuffer<TileGrid> g_TileGrid;
[[vk::binding(3, 0)]] RWStructuredBuffer<uint> g_GlobalIndexCounter;

[[vk::binding(0, 1)]]
cbuffer FrameParams
{
    float4x4 ViewMatrix;
    float4x4 InvProjectionMatrix;
    float4 ScreenResolution;
    float4 TileCount; // x: TilesX, y: TilesY, z: SlicesZ
    uint TotalLightCount;
    float NearClip;
    float FarClip;
    uint IsOrthographic;
};

groupshared uint s_TileLightCount;
groupshared uint s_TileLightIndexOffset;
groupshared uint s_TileLightList[128];

groupshared float3 s_ClusterMin;
groupshared float3 s_ClusterMax;

float3 UnprojectNDC(float2 ndc)
{
    float4 clip = float4(ndc.x, ndc.y, 1.0f, 1.0f);
    float4 view = mul(InvProjectionMatrix, clip);
    return view.xyz / view.w;
}

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(
    uint3 threadID : SV_DispatchThreadID,
    uint3 groupID : SV_GroupID,
    uint localIndex : SV_GroupIndex)
{
    uint3 clusterID = groupID;

    if (localIndex == 0)
    {
        s_TileLightCount = 0;

        float2 minNDC = (float2(clusterID.xy) / TileCount.xy) * 2.0f - 1.0f;
        float2 maxNDC = (float2(clusterID.xy + 1) / TileCount.xy) * 2.0f - 1.0f;

        float nearZ, farZ;
        if (TileCount.z <= 1.0f)
        {
            nearZ = NearClip;
            farZ = FarClip;
        }
        else
        {
            float sliceNear = (float)clusterID.z / TileCount.z;
            float sliceFar = (float)(clusterID.z + 1) / TileCount.z;

            if (IsOrthographic != 0)
            {
                nearZ = NearClip + sliceNear * (FarClip - NearClip);
                farZ = NearClip + sliceFar * (FarClip - NearClip);
            }
            else
            {
                nearZ = NearClip * pow(FarClip / NearClip, sliceNear);
                farZ = NearClip * pow(FarClip / NearClip, sliceFar);
            }
        }

        float3 p0 = UnprojectNDC(minNDC);
        float3 p1 = UnprojectNDC(float2(maxNDC.x, minNDC.y));
        float3 p2 = UnprojectNDC(float2(minNDC.x, maxNDC.y));
        float3 p3 = UnprojectNDC(maxNDC);

        float3 corners[8];
        corners[0] = p0 * (nearZ / p0.z);
        corners[1] = p1 * (nearZ / p1.z);
        corners[2] = p2 * (nearZ / p2.z);
        corners[3] = p3 * (nearZ / p3.z);
        corners[4] = p0 * (farZ / p0.z);
        corners[5] = p1 * (farZ / p1.z);
        corners[6] = p2 * (farZ / p2.z);
        corners[7] = p3 * (farZ / p3.z);

        float3 bMin = corners[0];
        float3 bMax = corners[0];
        for (int i = 1; i < 8; i++)
        {
            bMin = min(bMin, corners[i]);
            bMax = max(bMax, corners[i]);
        }

        s_ClusterMin = bMin;
        s_ClusterMax = bMax;
    }

    GroupMemoryBarrierWithGroupSync();

    for (uint lightIdx = localIndex; lightIdx < TotalLightCount; lightIdx += 64)
    {
        Light light = g_Lights[lightIdx];

        if (light.GetLightType() == 2)
        {
            uint slot;
            InterlockedAdd(s_TileLightCount, 1, slot);
            if (slot < 128)
            {
                s_TileLightList[slot] = lightIdx;
            }
            continue;
        }

        float4 lightViewPos = mul(ViewMatrix, float4(light.GetPosition(), 1.0f));
        float radius = light.GetRadius();

        if (SphereVsAABB(lightViewPos.xyz, radius, s_ClusterMin, s_ClusterMax))
        {
            uint slot;
            InterlockedAdd(s_TileLightCount, 1, slot);
            if (slot < 128)
            {
                s_TileLightList[slot] = lightIdx;
            }
        }
    }

    GroupMemoryBarrierWithGroupSync();

    if (localIndex == 0)
    {
        InterlockedAdd(g_GlobalIndexCounter[0], s_TileLightCount, s_TileLightIndexOffset);

        uint clusterFlatIndex = clusterID.x + clusterID.y * (uint)TileCount.x + clusterID.z * (uint)TileCount.x * (uint)TileCount.y;
        g_TileGrid[clusterFlatIndex].lightOffset = s_TileLightIndexOffset;
        g_TileGrid[clusterFlatIndex].lightCount = s_TileLightCount;
    }

    GroupMemoryBarrierWithGroupSync();

    for (uint i = localIndex; i < s_TileLightCount && i < 128; i += 64)
    {
        g_GlobalLightIndices[s_TileLightIndexOffset + i] = s_TileLightList[i];
    }
}