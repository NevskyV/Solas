using System.Numerics;
using Silk.NET.Vulkan;
using Solas.Render.Components;
using Solas.Render.Vulkan.Extensions;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanUniformBuffers : VulkanInjectable
{
    static float Radians(float angle) => angle * MathF.PI / 180f;

    internal void Create()
    {
        var bufferSize = (ulong)sizeof(UniformBufferObject);
        Ctx.UniformBuffers = new Buffer[Ctx.MaxFramesInFlight];
        Ctx.UniformBuffersMemory = new DeviceMemory[Ctx.MaxFramesInFlight];
        for (var i = 0; i < Ctx.MaxFramesInFlight; i++)
        {
            var (buffer, bufferMem) =
                Buffer.Create(Ctx, bufferSize, BufferUsageFlags.UniformBufferBit,
                    MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
            Ctx.UniformBuffers[i] = buffer;
            Ctx.UniformBuffersMemory[i] = bufferMem;
        }
    }

    internal void Update(uint currentImage)
    {
        var time = (float)Ctx.Window!.Time;

        var ubo = new UniformBufferObject()
        {
            Model = Matrix4x4.Identity * Matrix4x4.CreateFromAxisAngle(new Vector3(0, 0, 1), time * Radians(45.0f)),
            View = Matrix4x4.CreateLookAt(new Vector3(2, 2, 2), new Vector3(0, 0, 0), new Vector3(0, 0, 1)),
            Proj = Matrix4x4.CreatePerspectiveFieldOfView(Radians(45.0f),
                (float)Ctx.SwapChainExtent.Width / Ctx.SwapChainExtent.Height, 0.1f, 10.0f)
        };

        ubo.Proj.M22 *= -1;

        void* data;
        Ctx.Vk!.MapMemory(Ctx.Device, Ctx.UniformBuffersMemory![currentImage], 0, (ulong)sizeof(UniformBufferObject), 0,
            &data);
        new Span<UniformBufferObject>(data, 1)[0] = ubo;
        Ctx.Vk!.UnmapMemory(Ctx.Device, Ctx.UniformBuffersMemory![currentImage]);
    }
}