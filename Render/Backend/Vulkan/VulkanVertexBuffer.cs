using Silk.NET.Vulkan;
using Solas.Render.Components;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Solas.Render.Backend.Vulkan;

internal unsafe class VulkanVertexBuffer : VulkanInjectable
{
    internal void Create()
    {
        var bufferSize = (ulong)sizeof(Vertex) * (ulong)Ctx.Vertices.Length;
        var (stagingBuffer, stagingBufferMemory) =
            Buffer.Create(Ctx, bufferSize, BufferUsageFlags.TransferSrcBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        void* dataStaging;
        Ctx.Vk!.MapMemory(Ctx.Device, stagingBufferMemory, 0, bufferSize, 0, &dataStaging);
        Ctx.Vertices.AsSpan().CopyTo(new Span<Vertex>(dataStaging, Ctx.Vertices.Length));
        Ctx.Vk!.UnmapMemory(Ctx.Device, stagingBufferMemory);

        (Ctx.VertexBuffer, Ctx.VertexBufferMemory) =
            Buffer.Create(Ctx, bufferSize, BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit,
                MemoryPropertyFlags.DeviceLocalBit);

        Buffer.CopyBuffer(Ctx, stagingBuffer, Ctx.VertexBuffer, bufferSize);

        Ctx.Vk!.DestroyBuffer(Ctx.Device, stagingBuffer, null);
        Ctx.Vk!.FreeMemory(Ctx.Device, stagingBufferMemory, null);
    }
}