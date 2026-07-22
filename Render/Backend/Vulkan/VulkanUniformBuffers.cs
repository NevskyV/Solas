using System.Numerics;
using Silk.NET.Vulkan;
using Solas.Render.Components;
using Solas.Render.Vulkan.Extensions;
using Solas.Transform.MathExtensions;
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
        Vector3 cameraPos = Ctx.CameraTransform.Position.Value;
        Vector3 cameraRot = Ctx.CameraTransform.Rotation.Value;

        Quaternion cameraQuat = cameraRot.ToQuaternion();

        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, cameraQuat);
        Vector3 up = Vector3.Transform(Vector3.UnitY, cameraQuat);

        var ubo = new UniformBufferObject()
        {
            Model = Matrix4x4.Identity,

            View = Matrix4x4.CreateLookAt(cameraPos, cameraPos + forward, up),

            Proj = Matrix4x4.CreatePerspectiveFieldOfView(
                Radians(Ctx.CameraData.FieldOfView),
                (float)Ctx.SwapChainExtent.Width / Ctx.SwapChainExtent.Height,
                0.1f,
                100.0f
            )
        };

        ubo.Proj.M22 *= -1;

        void* data;
        Ctx.Vk!.MapMemory(Ctx.Device, Ctx.UniformBuffersMemory![currentImage], 0, (ulong)sizeof(UniformBufferObject), 0,
            &data);
        new Span<UniformBufferObject>(data, 1)[0] = ubo;
        Ctx.Vk!.UnmapMemory(Ctx.Device, Ctx.UniformBuffersMemory![currentImage]);
    }
}