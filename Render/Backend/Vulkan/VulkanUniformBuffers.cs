using System;
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
        data.UniformBuffers = new Buffer[Ctx.Settings.MaxFramesInFlight];
        data.UniformBuffersMemory = new DeviceMemory[Ctx.Settings.MaxFramesInFlight];

        var extraMaterialSize = (ulong)(data.Material?.BuildCombinedUboData().Length ?? 0);
        var bufferSize = (ulong)sizeof(UniformBufferObject) + extraMaterialSize;

        for (var i = 0; i < Ctx.Settings.MaxFramesInFlight; i++)
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

        for (var i = 0; i < Ctx.Settings.MaxFramesInFlight; i++)
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

        float aspectRatio = (float)Ctx.RenderExtent.Width / Ctx.RenderExtent.Height;

        Ctx.CameraViewMatrix = Matrix4x4.CreateLookAt(cameraPos, cameraPos + forward, up);
        Ctx.CameraProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
            Radians(Ctx.CameraData.FieldOfView),
            aspectRatio,
            Ctx.CameraData.NearClipPlane,
            Ctx.CameraData.FarClipPlane
        );

        Ctx.CameraProjectionMatrix.M22 *= -1;

        uint tileCountX = (uint)MathF.Ceiling(Ctx.RenderExtent.Width / (float)Ctx.Settings.TileSize);
        uint tileCountY = (uint)MathF.Ceiling(Ctx.RenderExtent.Height / (float)Ctx.Settings.TileSize);

        foreach (var renderer in Ctx.RenderData)
        {
            var ubo = new UniformBufferObject()
            {
                Model = Matrix4x4.Transpose(renderer.Logic.GetModelMatrix()),
                View = Matrix4x4.Transpose(Ctx.CameraViewMatrix),
                Proj = Matrix4x4.Transpose(Ctx.CameraProjectionMatrix),
                TileCount = new Vector2(tileCountX, tileCountY),
                TileSize = Ctx.Settings.TileSize
            };

            var extraBytes = renderer.Material?.BuildCombinedUboData() ?? [];
            var totalSize = (ulong)sizeof(UniformBufferObject) + (ulong)extraBytes.Length;

            void* data;
            Ctx.Vk!.MapMemory(Ctx.Device, renderer.UniformBuffersMemory[currentImage], 0, totalSize, 0, &data);

            new Span<UniformBufferObject>(data, 1)[0] = ubo;

            if (extraBytes.Length > 0)
            {
                byte* dstPtr = (byte*)data + sizeof(UniformBufferObject);
                fixed (byte* srcPtr = extraBytes)
                {
                    System.Buffer.MemoryCopy(srcPtr, dstPtr, extraBytes.Length, extraBytes.Length);
                }
            }

            Ctx.Vk!.UnmapMemory(Ctx.Device, renderer.UniformBuffersMemory[currentImage]);
        }
    }
}