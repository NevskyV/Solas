module MaterialScreen;

import MaterialInterfaces;

public struct VSOutput
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
};

public VSOutput screenVertMain(uint vertexID)
{
    VSOutput output;
    output.uv = float2((vertexID << 1) & 2, float(vertexID & 2));
    output.position = float4(output.uv * 2.0f - 1.0f, 0.0f, 1.0f);
    return output;
}

[[vk::binding(1, 1)]] public Sampler2D texture;

public float4 screenFragMain<TFragChain : IFragmentChain>(VSOutput input, TFragChain fragChain)
{
    float4 baseColor = texture.Sample(input.uv);
    return fragChain.ProcessChain(baseColor, input.uv, float3(0.0f, 0.0f, 0.0f), float3(0.0f, 0.0f, 1.0f));
}
