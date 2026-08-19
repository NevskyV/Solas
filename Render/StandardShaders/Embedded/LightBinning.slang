
import LightData;
import FrameParams;

[[vk::binding(0, 0)]] StructuredBuffer<Light> g_Lights;
[[vk::binding(0, 1)]] ConstantBuffer<FrameParamsData> FrameParams;
[[vk::binding(8, 0)]] RWStructuredBuffer<uint> g_CoarseTileLightCounts;
[[vk::binding(9, 0)]] RWStructuredBuffer<uint> g_CoarseTileLightIndices;

static const uint CoarseTileScale = 4u;
static const uint MaxCoarseLightsPerTile = 1024u;

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint lightIndex = min(FrameParams.DirectionalLightCount, FrameParams.TotalLightCount) + dispatchThreadID.x;
    if (lightIndex >= FrameParams.TotalLightCount)
    {
        return;
    }

    Light light = g_Lights[lightIndex];
    float radius = light.GetRadius();
    float3 directionToLight = light.GetPosition() - FrameParams.CameraPosition.xyz;
    float viewX = dot(directionToLight, FrameParams.CameraRight.xyz);
    float viewY = dot(directionToLight, FrameParams.CameraUp.xyz);
    float viewZ = dot(directionToLight, FrameParams.CameraForward.xyz);
    float nearClip = max(FrameParams.NearClip, 0.001f);
    float farClip = max(FrameParams.FarClip, nearClip + 0.001f);

    if (viewZ + radius < nearClip || viewZ - radius > farClip)
    {
        return;
    }

    float minNdcX;
    float maxNdcX;
    float minNdcY;
    float maxNdcY;
    if (FrameParams.IsOrthographic != 0u)
    {
        minNdcX = (viewX - radius) / FrameParams.TanHalfFovX;
        maxNdcX = (viewX + radius) / FrameParams.TanHalfFovX;
        minNdcY = (viewY - radius) / FrameParams.TanHalfFovY;
        maxNdcY = (viewY + radius) / FrameParams.TanHalfFovY;
    }
    else if (viewZ - radius <= nearClip)
    {
        minNdcX = -1.0f;
        maxNdcX = 1.0f;
        minNdcY = -1.0f;
        maxNdcY = 1.0f;
    }
    else
    {
        float closestDepth = max(viewZ - radius, nearClip);
        float maxAbsNdcX = (abs(viewX) + radius) / (closestDepth * FrameParams.TanHalfFovX);
        float maxAbsNdcY = (abs(viewY) + radius) / (closestDepth * FrameParams.TanHalfFovY);
        minNdcX = -maxAbsNdcX;
        maxNdcX = maxAbsNdcX;
        minNdcY = -maxAbsNdcY;
        maxNdcY = maxAbsNdcY;
    }

    minNdcX = clamp(minNdcX, -1.0f, 1.0f);
    maxNdcX = clamp(maxNdcX, -1.0f, 1.0f);
    minNdcY = clamp(minNdcY, -1.0f, 1.0f);
    maxNdcY = clamp(maxNdcY, -1.0f, 1.0f);

    uint tileCountX = (uint)FrameParams.TileCount.x;
    uint tileCountY = (uint)FrameParams.TileCount.y;
    uint coarseTileCountX = (tileCountX + CoarseTileScale - 1u) / CoarseTileScale;
    uint coarseTileCountY = (tileCountY + CoarseTileScale - 1u) / CoarseTileScale;
    uint minTileX = (uint)clamp(floor((minNdcX * 0.5f + 0.5f) * float(coarseTileCountX)), 0.0f,
        float(coarseTileCountX - 1u));
    uint maxTileX = (uint)clamp(floor((maxNdcX * 0.5f + 0.5f) * float(coarseTileCountX)), 0.0f,
        float(coarseTileCountX - 1u));
    uint minTileY = (uint)clamp(floor((minNdcY * 0.5f + 0.5f) * float(coarseTileCountY)), 0.0f,
        float(coarseTileCountY - 1u));
    uint maxTileY = (uint)clamp(floor((maxNdcY * 0.5f + 0.5f) * float(coarseTileCountY)), 0.0f,
        float(coarseTileCountY - 1u));
    minTileX = minTileX > 0u ? minTileX - 1u : 0u;
    minTileY = minTileY > 0u ? minTileY - 1u : 0u;
    maxTileX = min(maxTileX + 1u, coarseTileCountX - 1u);
    maxTileY = min(maxTileY + 1u, coarseTileCountY - 1u);

    for (uint tileY = minTileY; tileY <= maxTileY; ++tileY)
    {
        for (uint tileX = minTileX; tileX <= maxTileX; ++tileX)
        {
            uint coarseTileIndex = tileX + tileY * coarseTileCountX;
            uint writeIndex = 0u;
            InterlockedAdd(g_CoarseTileLightCounts[coarseTileIndex], 1u, writeIndex);
            if (writeIndex < MaxCoarseLightsPerTile)
            {
                g_CoarseTileLightIndices[coarseTileIndex * MaxCoarseLightsPerTile + writeIndex] = lightIndex;
            }
        }
    }
}
