
import LightData;
import FrameParams;

public struct TileGrid
{
    public uint lightOffset;
    public uint lightCount;
};

[[vk::binding(0, 0)]] StructuredBuffer<Light> g_Lights;
[[vk::binding(0, 1)]] ConstantBuffer<FrameParamsData> FrameParams;
[[vk::binding(1, 0)]] RWStructuredBuffer<uint> g_GlobalLightIndices;
[[vk::binding(2, 0)]] RWStructuredBuffer<TileGrid> g_TileGrid;
[[vk::binding(3, 0)]] RWStructuredBuffer<uint> g_GlobalIndexCounter;
[[vk::binding(8, 0)]] StructuredBuffer<uint> g_CoarseTileLightCounts;
[[vk::binding(9, 0)]] StructuredBuffer<uint> g_CoarseTileLightIndices;

groupshared uint s_Visibility[64];
groupshared uint s_ClusterIndices[128];
groupshared uint s_ClusterLightCount;
groupshared uint s_ClusterBaseOffset;

static const uint CoarseTileScale = 4u;
static const uint MaxCoarseLightsPerTile = 1024u;

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 groupID : SV_GroupID, uint3 groupThreadID : SV_GroupThreadID)
{
    uint3 clusterID = groupID.xyz;
    uint laneIndex = groupThreadID.x;
    uint tileCountX = (uint)FrameParams.TileCount.x;
    uint tileCountY = (uint)FrameParams.TileCount.y;
    uint tileCountZ = (uint)FrameParams.TileCount.z;
    uint clusterFlatIndex = clusterID.x + clusterID.y * tileCountX + clusterID.z * tileCountX * tileCountY;
    uint coarseTileCountX = (tileCountX + CoarseTileScale - 1u) / CoarseTileScale;
    uint coarseTileX = clusterID.x / CoarseTileScale;
    uint coarseTileY = clusterID.y / CoarseTileScale;
    uint coarseTileIndex = coarseTileX + coarseTileY * coarseTileCountX;

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
        float sliceFar = float(clusterID.z + 1u) / float(tileCountZ);

        if (FrameParams.IsOrthographic != 0u)
        {
            clusterMinZ = nClip + sliceNear * (fClip - nClip);
            clusterMaxZ = nClip + sliceFar * (fClip - nClip);
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

    if (laneIndex == 0u)
    {
        s_ClusterLightCount = 0u;
        s_ClusterBaseOffset = 0u;
    }

    GroupMemoryBarrierWithGroupSync();

    uint coarseLightCount = min(g_CoarseTileLightCounts[coarseTileIndex], MaxCoarseLightsPerTile);
    uint coarseLightOffset = coarseTileIndex * MaxCoarseLightsPerTile;
    for (uint batchStart = 0u; batchStart < coarseLightCount; batchStart += 64u)
    {
        if (s_ClusterLightCount >= 128u)
        {
            break;
        }

        uint coarseLightIndex = batchStart + laneIndex;
        uint isOverlapping = 0u;
        uint lightIndex = 0u;
        if (coarseLightIndex < coarseLightCount)
        {
            lightIndex = g_CoarseTileLightIndices[coarseLightOffset + coarseLightIndex];
            Light light = g_Lights[lightIndex];
            float3 lightWorldPos = light.GetPosition();
            float radius = light.GetRadius();
            float3 directionToLight = lightWorldPos - FrameParams.CameraPosition.xyz;
            float viewX = dot(directionToLight, FrameParams.CameraRight.xyz);
            float viewY = dot(directionToLight, FrameParams.CameraUp.xyz);
            float viewZ = dot(directionToLight, FrameParams.CameraForward.xyz);

            if (viewZ + radius >= clusterMinZ && viewZ - radius <= clusterMaxZ)
            {
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
                float distanceSquared = dx * dx + dy * dy + dz * dz;
                isOverlapping = distanceSquared <= radius * radius ? 1u : 0u;
            }
        }

        s_Visibility[laneIndex] = isOverlapping;
        GroupMemoryBarrierWithGroupSync();

        if (laneIndex == 0u)
        {
            uint availableCount = 128u - s_ClusterLightCount;
            for (uint batchIndex = 0u; batchIndex < 64u; ++batchIndex)
            {
                if (s_Visibility[batchIndex] != 0u && availableCount > 0u)
                {
                    s_ClusterIndices[s_ClusterLightCount] =
                        g_CoarseTileLightIndices[coarseLightOffset + batchStart + batchIndex];
                    s_ClusterLightCount++;
                    availableCount--;
                }
            }
        }

        GroupMemoryBarrierWithGroupSync();
    }

    if (laneIndex == 0u)
    {
        if (s_ClusterLightCount > 0u)
        {
            InterlockedAdd(g_GlobalIndexCounter[0], s_ClusterLightCount, s_ClusterBaseOffset);
            g_TileGrid[clusterFlatIndex].lightOffset = s_ClusterBaseOffset;
            g_TileGrid[clusterFlatIndex].lightCount = s_ClusterLightCount;
        }
        else
        {
            g_TileGrid[clusterFlatIndex].lightOffset = 0u;
            g_TileGrid[clusterFlatIndex].lightCount = 0u;
        }
    }

    GroupMemoryBarrierWithGroupSync();

    for (uint outputIndex = laneIndex; outputIndex < s_ClusterLightCount; outputIndex += 64u)
    {
        g_GlobalLightIndices[s_ClusterBaseOffset + outputIndex] = s_ClusterIndices[outputIndex];
    }
}
