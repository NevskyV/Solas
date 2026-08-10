module Material3D;

import MaterialInterfaces;
import Lighting;

public struct VSInput
{
    float3 inPosition : POSITION;
    float3 inNormal : NORMAL;
    float2 inTexCoord : TEXCOORD0;
};

public struct UniformBuffer
{
    public float4x4 model;
    public float4x4 view;
    public float4x4 proj;
    public float2 tileCount;
    public float1 tileSize;
};

[[vk::binding(0, 1)]] public ConstantBuffer<UniformBuffer> ubo;

public struct VSOutput
{
    float4 pos : SV_Position;
    float3 worldPos : TEXCOORD1;
    float3 worldNormal : NORMAL;
    float2 fragTexCoord : TEXCOORD0;
};

[shader("vertex")]
public VSOutput vertMain<TVertChain : IVertexChain>(VSInput input, TVertChain vertChain)
{
    VSOutput output;
    float3 modPos = vertChain.ProcessChain(input.inPosition, input.inNormal, input.inTexCoord, 0.0f);
    float4 worldPos4 = mul(ubo.model, float4(modPos, 1.0));
    output.worldPos = worldPos4.xyz;

    float3x3 normalMatrix = (float3x3)ubo.model;
    output.worldNormal = normalize(mul(normalMatrix, input.inNormal));
    output.pos = mul(ubo.proj, mul(ubo.view, worldPos4));
    output.fragTexCoord = input.inTexCoord;

    return output;
}

[shader("fragment")]
public float4 fragMain<TFragChain : IFragmentChain>(VSOutput vertIn, TFragChain fragChain) : SV_Target
{
    float4 initialColor = float4(1.0f, 1.0f, 1.0f, 1.0f);
    float3 N = normalize(vertIn.worldNormal);

    return fragChain.ProcessChain(initialColor, vertIn.fragTexCoord, vertIn.worldPos, N);
}
