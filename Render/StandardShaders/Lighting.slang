import FrameParams;
import LightData;
import ShadowData;

public struct TileGrid
{
    public uint lightOffset;
    public uint lightCount;
};

[[vk::binding(0, 0)]] public StructuredBuffer<Light> g_Lights;
[[vk::binding(1, 0)]] public StructuredBuffer<uint> g_GlobalLightIndices;
[[vk::binding(2, 0)]] public StructuredBuffer<TileGrid> g_TileGrid;
[[vk::binding(4, 0)]] public Sampler2DArrayShadow g_ShadowDepthArray;
[[vk::binding(5, 0)]] public SamplerCubeArrayShadow g_PointShadowCubeArray;
[[vk::binding(6, 0)]] public StructuredBuffer<ShadowMatrixData> g_ShadowMatrices;
[[vk::binding(7, 0)]] public ConstantBuffer<FrameParamsData> g_ShadowFrameParams;
