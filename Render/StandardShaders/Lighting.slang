module Lighting;

import LightData;

public struct TileGrid
{
    public uint lightOffset;
    public uint lightCount;
};

[[vk::binding(0, 0)]] public StructuredBuffer<Light> g_Lights;
[[vk::binding(1, 0)]] public StructuredBuffer<uint> g_GlobalLightIndices;
[[vk::binding(2, 0)]] public StructuredBuffer<TileGrid> g_TileGrid;