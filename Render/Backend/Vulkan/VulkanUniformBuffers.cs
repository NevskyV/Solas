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

    internal void CreateForObject(VulkanRenderData data)
    {
        data.UniformBuffers = new Buffer[Ctx.MaxFramesInFlight];
        data.UniformBuffersMemory = new DeviceMemory[Ctx.MaxFramesInFlight];

        var bufferSize = (ulong)sizeof(UniformBufferObject);
        for (var i = 0; i < Ctx.MaxFramesInFlight; i++)
        {
            var (buffer, bufferMem) = Buffer.Create(Ctx, bufferSize, BufferUsageFlags.UniformBufferBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
            data.UniformBuffers[i] = buffer;
            data.UniformBuffersMemory[i] = bufferMem;
        }
    }

    internal void DestroyForObject(VulkanRenderData data)
    {
        if (data.UniformBuffers == null) return;

        for (var i = 0; i < Ctx.MaxFramesInFlight; i++)
        {
            Ctx.Vk!.DestroyBuffer(Ctx.Device, data.UniformBuffers[i], null);
            Ctx.Vk!.FreeMemory(Ctx.Device, data.UniformBuffersMemory[i], null);
        }

        data.UniformBuffers = null!;
        data.UniformBuffersMemory = null!;
    }

    internal void Update(uint currentImage)
    {
        Vector3 cameraPos = Ctx.CameraTransform.Position.Value;
        Vector3 cameraRot = Ctx.CameraTransform.Rotation.Value;
        Quaternion cameraQuat = cameraRot.ToQuaternion();

        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, cameraQuat);
        Vector3 up = Vector3.Transform(Vector3.UnitY, cameraQuat);

        float aspectRatio = (float)Ctx.SwapChainExtent.Width / Ctx.SwapChainExtent.Height;

        Ctx.CameraViewMatrix = Matrix4x4.CreateLookAt(cameraPos, cameraPos + forward, up);
        Ctx.CameraProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
            Radians(Ctx.CameraData.FieldOfView),
            aspectRatio,
            0.1f,
            100.0f
        );

        Ctx.CameraProjectionMatrix.M22 *= -1;

        uint tileCountX = (uint)MathF.Ceiling(Ctx.SwapChainExtent.Width / 16.0f);
        uint tileCountY = (uint)MathF.Ceiling(Ctx.SwapChainExtent.Height / 16.0f);

        foreach (var renderer in Ctx.RenderData)
        {
            var ubo = new UniformBufferObject()
            {
                Model = renderer.Logic.GetModelMatrix(),
                View = Ctx.CameraViewMatrix,
                Proj = Ctx.CameraProjectionMatrix,
                TileCount = new Vector2(tileCountX, tileCountY)
            };

            void* data;
            Ctx.Vk!.MapMemory(Ctx.Device, renderer.UniformBuffersMemory[currentImage], 0,
                (ulong)sizeof(UniformBufferObject), 0, &data);
            new Span<UniformBufferObject>(data, 1)[0] = ubo;
            Ctx.Vk!.UnmapMemory(Ctx.Device, renderer.UniformBuffersMemory[currentImage]);
        }
    }
}