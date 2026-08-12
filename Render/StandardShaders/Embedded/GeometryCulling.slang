module GeometryCulling;

public struct ObjectData
{
    public float4 WorldCenterAndRadius;
};

public struct DrawIndexedIndirectCommand
{
    public uint indexCount;
    public uint instanceCount;
    public uint firstIndex;
    public int vertexOffset;
    public uint firstInstance;
};

[[vk::binding(0, 0)]] StructuredBuffer<ObjectData> g_Objects;
[[vk::binding(1, 0)]] RWStructuredBuffer<DrawIndexedIndirectCommand> g_IndirectCommands;

struct FrameParamsData
{
    float4x4 LightViewProj;
    float4 CameraPosition;
    float4 CameraRight;
    float4 CameraUp;
    float4 CameraForward;
    float4 ScreenResolution;
    float4 TileCount;
    uint TotalLightCount;
    uint DirectionalLightCount;
    float NearClip;
    float FarClip;
    uint IsOrthographic;
    float TanHalfFovX;
    float TanHalfFovY;
    float _pad;
};

[[vk::binding(0, 1)]] ConstantBuffer<FrameParamsData> FrameParams;

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 threadID : SV_DispatchThreadID)
{
    uint objectID = threadID.x;
    uint numObjects, stride;
    g_Objects.GetDimensions(numObjects, stride);

    if (objectID >= numObjects)
        return;

    ObjectData obj = g_Objects[objectID];

    float3 worldCenter = obj.WorldCenterAndRadius.xyz;
    float radius = obj.WorldCenterAndRadius.w;

    float3 dir = worldCenter - FrameParams.CameraPosition.xyz;
    float viewX = dot(dir, FrameParams.CameraRight.xyz);
    float viewY = dot(dir, FrameParams.CameraUp.xyz);
    float viewZ = dot(dir, FrameParams.CameraForward.xyz);

    float nClip = max(FrameParams.NearClip, 0.001f);
    float fClip = max(FrameParams.FarClip, nClip + 0.001f);

    float tanX = FrameParams.TanHalfFovX;
    float tanY = FrameParams.TanHalfFovY;
    float secX = sqrt(1.0f + tanX * tanX);
    float secY = sqrt(1.0f + tanY * tanY);

    bool isVisible = true;

    if (viewZ < nClip - radius || viewZ > fClip + radius)
    {
        isVisible = false;
    }
    else if (viewZ <= nClip && length(float3(viewX, viewY, viewZ)) > radius)
    {
        isVisible = false;
    }
    else
    {
        float evalZ = max(viewZ, nClip);
        float maxLimitX = evalZ * tanX + radius * secX;
        float minLimitX = -evalZ * tanX - radius * secX;

        if (viewX < minLimitX || viewX > maxLimitX)
        {
            isVisible = false;
        }
        else
        {
            float maxLimitY = evalZ * tanY + radius * secY;
            float minLimitY = -evalZ * tanY - radius * secY;

            if (viewY < minLimitY || viewY > maxLimitY)
            {
                isVisible = false;
            }
        }
    }

    g_IndirectCommands[objectID].instanceCount = isVisible ? 1u : 0u;
}
