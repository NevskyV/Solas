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
    float4 TileCount;
    uint TotalLightCount;
    float NearClip;
    float FarClip;
    uint IsOrthographic;
};

groupshared uint s_TileLightCount;
groupshared uint s_TileLightIndexOffset;
groupshared uint s_TileLightList[128];

float3 UnprojectNDC(float2 ndc)
{
    float4 clip = float4(ndc.x, ndc.y, 1.0f, 1.0f);
    float4 view = mul(InvProjectionMatrix, clip);
    return normalize(view.xyz / view.w);
}

[shader("compute")]
[numthreads(64, 1, 1)]
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

    uint3 clusterID = groupID.xyz;

    float2 pxMin = (float2(clusterID.xy) / TileCount.xy) * 2.0f - 1.0f;
    float2 pxMax = (float2(clusterID.xy + uint2(1, 1)) / TileCount.xy) * 2.0f - 1.0f;

    float3 v00 = UnprojectNDC(float2(pxMin.x, pxMin.y));
    float3 v10 = UnprojectNDC(float2(pxMax.x, pxMin.y));
    float3 v01 = UnprojectNDC(float2(pxMin.x, pxMax.y));
    float3 v11 = UnprojectNDC(float2(pxMax.x, pxMax.y));

    Plane frustumPlanes[4];
    frustumPlanes[0].normal = normalize(cross(v01, v00));
    frustumPlanes[1].normal = normalize(cross(v10, v11));
    frustumPlanes[2].normal = normalize(cross(v00, v10));
    frustumPlanes[3].normal = normalize(cross(v11, v01));

    float minDepth;
    float maxDepth;

    if (TileCount.z <= 1.0f)
    {
        minDepth = NearClip;
        maxDepth = FarClip;
    }
    else
    {
        float zSlice = float(clusterID.z);
        float numSlices = TileCount.z;

        if (IsOrthographic != 0u)
        {
            minDepth = NearClip + (FarClip - NearClip) * (zSlice / numSlices);
            maxDepth = NearClip + (FarClip - NearClip) * ((zSlice + 1.0f) / numSlices);
        }
        else
        {
            minDepth = NearClip * pow(max(FarClip / max(NearClip, 0.001f), 1.001f), zSlice / numSlices);
            maxDepth = NearClip * pow(max(FarClip / max(NearClip, 0.001f), 1.001f), (zSlice + 1.0f) / numSlices);
        }
    }

    uint lightCountLimit = min(TotalLightCount, 1024u);

    for (uint lightIdx = localIndex; lightIdx < lightCountLimit; lightIdx += 64)
    {
        Light light = g_Lights[lightIdx];
        
        bool inFrustum = false;
        uint lightType = light.GetLightType();

        if (lightType == 2)
        {
            inFrustum = true;
        }
        else
        {
            float4 viewPos = mul(ViewMatrix, float4(light.GetPosition(), 1.0f));
            float radius = light.GetRadius();

            inFrustum = true;
            for (int p = 0; p < 4; p++)
            {
                if (!SphereVsPlane(viewPos.xyz, radius, frustumPlanes[p]))
                {
                    inFrustum = false;
                    break;
                }
            }

            if (inFrustum)
            {
                float lightDepth = -viewPos.z;
                if ((lightDepth + radius < minDepth) || (lightDepth - radius > maxDepth))
                {
                    inFrustum = false;
                }
            }
        }

        if (inFrustum)
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

    for (uint i = localIndex; i < s_TileLightCount; i += 64)
    {
        g_GlobalLightIndices[s_TileLightIndexOffset + i] = s_TileLightList[i];
    }
}