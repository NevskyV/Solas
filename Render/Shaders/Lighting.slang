module Lighting;

import PointLight;

public struct TileGrid
{
    public uint lightOffset;
    public uint lightCount;
};

[[vk::binding(0, 0)]] StructuredBuffer<PointLight> g_Lights;
[[vk::binding(1, 0)]] StructuredBuffer<uint> g_GlobalLightIndices;
[[vk::binding(2, 0)]] StructuredBuffer<TileGrid> g_TileGrid;

public uint GetTileLightCount(float2 screenPixelPos, uint2 tileCount)
{
    uint2 tileID = uint2(screenPixelPos) / 16;
    uint tileIndex = tileID.x + tileID.y * tileCount.x;
    return g_TileGrid[tileIndex].lightCount;
}

public float3 EvaluateForwardPlusLighting(float2 screenPixelPos, float3 worldPos, float3 normal, uint2 tileCount)
{
    uint2 tileID = uint2(screenPixelPos) / 16;
    uint tileIndex = tileID.x + tileID.y * tileCount.x;

    TileGrid tile = g_TileGrid[tileIndex];
    float3 finalColor = float3(0.0f, 0.0f, 0.0f);

    for (uint i = 0; i < tile.lightCount; i++)
    {
        uint globalLightIndex = g_GlobalLightIndices[tile.lightOffset + i];
        PointLight light = g_Lights[globalLightIndex];

        finalColor += light.CalculateLighting(worldPos, normal);
    }

    return finalColor;
}