using Silk.NET.Vulkan;
using Solas.Render.Vulkan.Extensions;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanShaderStorageBuffer : VulkanInjectable
{
    internal void Create()
    {
        Ctx.ShaderStorageBuffers = new Buffer[Ctx.MaxFramesInFlight];
        Ctx.ShaderStorageBuffersMemory = new DeviceMemory[Ctx.MaxFramesInFlight];

        var input = new uint[1024];
        for (uint i = 0; i < 1024; i++)
        {
            input[i] = i;
        }

        uint bufferSize = sizeof(uint) * 1024;

        var (stagingBuffer, stagingBufferMemory) = Buffer.Create(Ctx, bufferSize, BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        void* data;
        Ctx.Vk!.MapMemory(Ctx.Device, stagingBufferMemory, 0, bufferSize, 0, &data);
        fixed (uint* inputPtr = input)
        {
            System.Buffer.MemoryCopy(inputPtr, data, bufferSize, bufferSize);
        }

        Ctx.Vk!.UnmapMemory(Ctx.Device, stagingBufferMemory);

        for (var i = 0; i < Ctx.MaxFramesInFlight; i++)
        {
            var (shaderStorageBufferTemp, shaderStorageBufferTempMemory) =
                Buffer.Create(Ctx, bufferSize, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.VertexBufferBit |
                                               BufferUsageFlags.TransferDstBit | BufferUsageFlags.TransferSrcBit,
                    MemoryPropertyFlags.DeviceLocalBit);
            Buffer.CopyBuffer(Ctx, stagingBuffer, shaderStorageBufferTemp, bufferSize);
            Ctx.ShaderStorageBuffers[i] = shaderStorageBufferTemp;
            Ctx.ShaderStorageBuffersMemory[i] = shaderStorageBufferTempMemory;
        }
    }
}