using Silk.NET.Vulkan;
using Solas.Render.Components;
using Solas.Render.Vulkan.Components;
using Solas.Render.Vulkan.Extensions;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Solas.Render.Vulkan;

internal static unsafe class VulkanMeshLoader
{
    internal static MeshGpu Upload(VulkanContext ctx, Mesh mesh)
    {
        var vertexBufferSize = (ulong)sizeof(Vertex) * (ulong)mesh.Vertices.Length;
        var (vertexStagingBuffer, vertexStagingMemory) = Buffer.Create(ctx, vertexBufferSize,
            BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        void* vertexData;
        ctx.Vk!.MapMemory(ctx.Device, vertexStagingMemory, 0, vertexBufferSize, 0, &vertexData);
        mesh.Vertices.AsSpan().CopyTo(new Span<Vertex>(vertexData, mesh.Vertices.Length));
        ctx.Vk!.UnmapMemory(ctx.Device, vertexStagingMemory);

        var (vertexBuffer, vertexBufferMemory) = Buffer.Create(ctx, vertexBufferSize,
            BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit, MemoryPropertyFlags.DeviceLocalBit);
        Buffer.CopyBuffer(ctx, vertexStagingBuffer, vertexBuffer, vertexBufferSize);

        ctx.Vk!.DestroyBuffer(ctx.Device, vertexStagingBuffer, null);
        ctx.Vk!.FreeMemory(ctx.Device, vertexStagingMemory, null);

        var indexBufferSize = sizeof(uint) * (ulong)mesh.Indices.Length;
        var (indexStagingBuffer, indexStagingMemory) = Buffer.Create(ctx, indexBufferSize,
            BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        void* indexData;
        ctx.Vk!.MapMemory(ctx.Device, indexStagingMemory, 0, indexBufferSize, 0, &indexData);
        mesh.Indices.AsSpan().CopyTo(new Span<uint>(indexData, mesh.Indices.Length));
        ctx.Vk!.UnmapMemory(ctx.Device, indexStagingMemory);

        var (indexBuffer, indexBufferMemory) = Buffer.Create(ctx, indexBufferSize,
            BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit, MemoryPropertyFlags.DeviceLocalBit);
        Buffer.CopyBuffer(ctx, indexStagingBuffer, indexBuffer, indexBufferSize);

        ctx.Vk!.DestroyBuffer(ctx.Device, indexStagingBuffer, null);
        ctx.Vk!.FreeMemory(ctx.Device, indexStagingMemory, null);

        return new MeshGpu(ctx.Vk, ctx.Device, vertexBuffer, vertexBufferMemory, indexBuffer, indexBufferMemory,
            (uint)mesh.Indices.Length);
    }
}