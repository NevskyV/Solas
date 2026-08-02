import Lighting;

struct VSInput {
    float3 inPosition;
    float3 inNormal;
    float2 inTexCoord;
};

struct UniformBuffer {
    float4x4 model;
    float4x4 view;
    float4x4 proj;
    float2 tileCount;
    float1 tileSize;
};

[[vk::binding(0, 1)]] ConstantBuffer<UniformBuffer> ubo;
[[vk::binding(1, 1)]] Sampler2D texture;

struct VSOutput
{
    float4 pos : SV_Position;
    float3 worldPos;
    float3 worldNormal;
    float2 fragTexCoord;
};

[shader("vertex")]
VSOutput vertMain(VSInput input) {
    VSOutput output;
    
    float4 worldPos4 = mul(ubo.model, float4(input.inPosition, 1.0));
    output.worldPos = worldPos4.xyz;
    
    float3x3 normalMatrix = (float3x3)ubo.model;
    output.worldNormal = normalize(mul(normalMatrix, input.inNormal));

    output.pos = mul(ubo.proj, mul(ubo.view, worldPos4));
    output.fragTexCoord = input.inTexCoord;
    
    return output;
}

[shader("fragment")]
float4 fragMain(VSOutput vertIn) : SV_TARGET {
    float4 texColor = texture.Sample(vertIn.fragTexCoord);
    
    float2 screenPixelPos = vertIn.pos.xy;
    float3 N = normalize(vertIn.worldNormal);
    uint2 tileCount = uint2(ubo.tileCount);
    
    float3 lightColor = EvaluateForwardPlusLighting(screenPixelPos, vertIn.worldPos, N, tileCount, ubo.tileSize);
    
    return float4(texColor.rgb * lightColor, texColor.a);
}