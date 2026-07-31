using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Solas.Render.Vulkan.Components;

internal unsafe class VulkanGpuMesh : IDisposable
{
    private readonly Vk _vk;
    private readonly Device _device;

    internal Buffer VertexBuffer;
    internal DeviceMemory VertexBufferMemory;
    internal Buffer IndexBuffer;
    internal DeviceMemory IndexBufferMemory;
    internal uint IndexCount;

    internal VulkanGpuMesh(Vk vk, Device device, Buffer vertexBuffer, DeviceMemory vertexMemory, Buffer indexBuffer,
        DeviceMemory indexMemory, uint indexCount)
    {
        _vk = vk;
        _device = device;
        VertexBuffer = vertexBuffer;
        VertexBufferMemory = vertexMemory;
        IndexBuffer = indexBuffer;
        IndexBufferMemory = indexMemory;
        IndexCount = indexCount;
    }

    public void Dispose()
    {
        _vk.DestroyBuffer(_device, IndexBuffer, null);
        _vk.FreeMemory(_device, IndexBufferMemory, null);
        _vk.DestroyBuffer(_device, VertexBuffer, null);
        _vk.FreeMemory(_device, VertexBufferMemory, null);
    }
}