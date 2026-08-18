import ShadowData;

public struct ShadowDepthVertexInput
{
    float3 position : POSITION;
    float3 normal : NORMAL;
    float2 texCoord : TEXCOORD0;
};

public struct ShadowDepthObjectUniform
{
    public float4x4 model;
};

[[vk::binding(0, 1)]] ConstantBuffer<ShadowDepthObjectUniform> ShadowObject;
[[vk::binding(6, 0)]] StructuredBuffer<ShadowMatrixData> g_ShadowMatrices;

[[vk::push_constant]]
public cbuffer ShadowDepthPass
{
    public uint shadowMatrixIndex;
};

[shader("vertex")]
public float4 vertexMain(ShadowDepthVertexInput input) : SV_Position
{
    float4 worldPosition = mul(ShadowObject.model, float4(input.position, 1.0f));
    return mul(g_ShadowMatrices[shadowMatrixIndex].LightViewProj, worldPosition);
}
