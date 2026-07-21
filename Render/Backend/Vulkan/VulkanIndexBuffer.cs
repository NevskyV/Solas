using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Solas.Render.Backend.Vulkan;

internal unsafe class VulkanIndexBuffer : VulkanInjectable
{
    internal void Create()
    {
        var bufferSize = sizeof(uint) * (ulong)Ctx.Indices.Length;
        var (stagingBuffer, stagingBufferMemory) =
            Buffer.Create(Ctx, bufferSize, BufferUsageFlags.TransferSrcBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        void* dataStaging;
        Ctx.Vk!.MapMemory(Ctx.Device, stagingBufferMemory, 0, bufferSize, 0, &dataStaging);
        Ctx.Indices.AsSpan().CopyTo(new Span<uint>(dataStaging, Ctx.Indices.Length));
        Ctx.Vk!.UnmapMemory(Ctx.Device, stagingBufferMemory);

        (Ctx.IndexBuffer, Ctx.IndexBufferMemory) =
            Buffer.Create(Ctx, bufferSize, BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit,
                MemoryPropertyFlags.DeviceLocalBit);

        Buffer.CopyBuffer(Ctx, stagingBuffer, Ctx.IndexBuffer, bufferSize);

        Ctx.Vk!.DestroyBuffer(Ctx.Device, stagingBuffer, null);
        Ctx.Vk!.FreeMemory(Ctx.Device, stagingBufferMemory, null);
    }
}