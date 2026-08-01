[[vk::binding(0, 0)]]
RWStructuredBuffer<uint> DataBuffer;

[shader("compute")]
[numthreads(64, 1, 1)]
void compMain(uint3 threadID : SV_DispatchThreadID)
{
    uint index = threadID.x;
    if (index < 1024)
    {
        DataBuffer[index] = DataBuffer[index] * 2;
    }
}