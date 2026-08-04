module Material2D;

import MaterialInterfaces;

[[vk::binding(0, 0)]]
public cbuffer FrameData
{
    float4x4 ViewProjection;
    float Time;
};

[[vk::binding(0, 1)]] public Texture2D g_MainTexture;
[[vk::binding(1, 1)]] public SamplerState g_MainSampler;

public struct VSInput
{
    float3 position : POSITION;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
};

public struct VSOutput
{
    float4 clipPosition : SV_Position;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
};

[shader("vertex")]
public VSOutput vertMain<TVertChain : IVertexChain>(VSInput input, TVertChain vertChain)
{
    VSOutput output;
    float3 pos = vertChain.ProcessChain(input.position, float3(0, 0, 1), input.uv, Time);
    output.clipPosition = mul(ViewProjection, float4(pos, 1.0f));
    output.uv = input.uv;
    output.color = input.color;
    return output;
}

[shader("fragment")]
public float4 fragMain<TFragChain : IFragmentChain>(VSOutput input, TFragChain fragChain) : SV_Target
{
    float4 baseColor = g_MainTexture.Sample(g_MainSampler, input.uv) * input.color;
    return fragChain.ProcessChain(baseColor, input.uv, float3(0.0f, 0.0f, 0.0f), float3(0.0f, 0.0f, 1.0f));
}
