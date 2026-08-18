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
        var materialParamsOffset = GetMaterialParamsOffset();
        var bufferSize = materialParamsOffset + extraMaterialSize;

        for (var i = 0; i < Ctx.Settings.MaxFramesInFlight; i++)
        {
            var (buffer, bufferMem) = Buffer.Create(Ctx, bufferSize, BufferUsageFlags.UniformBufferBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
            data.UniformBuffers[i] = buffer;
            data.UniformBuffersMemory[i] = bufferMem;
        }
    }

    private ulong GetMaterialParamsOffset()
    {
        var alignment =
            Math.Max(Ctx.Vk!.GetPhysicalDeviceProperties(Ctx.PhysicalDevice).Limits.MinUniformBufferOffsetAlignment,
                1ul);
        var baseSize = (ulong)sizeof(UniformBufferObject);
        return (baseSize + alignment - 1ul) / alignment * alignment;
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
        var cameraPos = Ctx.CameraTransform.Position.Value;
        var cameraRot = Ctx.CameraTransform.Rotation.Value;
        var cameraQuat = cameraRot.ToQuaternion();

        var forward = Vector3.Transform(-Vector3.UnitZ, cameraQuat);
        var up = Vector3.Transform(Vector3.UnitY, cameraQuat);

        Ctx.CameraViewMatrix = Matrix4x4.CreateLookAt(cameraPos, cameraPos + forward, up);

        var aspectRatio = (float)Ctx.RenderExtent.Width / Ctx.RenderExtent.Height;

        if (Ctx.CameraData.Type == CameraType.Perspective)
        {
            Ctx.CameraProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
                Radians(Ctx.CameraData.FieldOfView),
                aspectRatio,
                Ctx.CameraData.NearClipPlane,
                Ctx.CameraData.FarClipPlane
            );
        }
        else
        {
            Ctx.CameraProjectionMatrix = Matrix4x4.CreateOrthographic(
                Ctx.CameraData.Size * aspectRatio,
                Ctx.CameraData.Size,
                Ctx.CameraData.NearClipPlane,
                Ctx.CameraData.FarClipPlane
            );
        }

        Ctx.CameraProjectionMatrix.M22 *= -1;

        var tileSizeX = Ctx.Settings.TileSize.Z > 1 ? Ctx.Settings.TileSize.X * 4f : Ctx.Settings.TileSize.X;
        var tileSizeY = Ctx.Settings.TileSize.Z > 1 ? Ctx.Settings.TileSize.Y * 4f : Ctx.Settings.TileSize.Y;

        var tileCountX = (uint)MathF.Ceiling(Ctx.RenderExtent.Width / tileSizeX);
        var tileCountY = (uint)MathF.Ceiling(Ctx.RenderExtent.Height / tileSizeY);
        var tileCountZ = (uint)Math.Max(1, (int)Ctx.Settings.TileSize.Z);

        var isOrtho = Ctx.CameraData.Type == CameraType.Orthographic;
        var activeLights = LightDataEventHandler.GetGpuLights(out var directionalCount);

        foreach (var renderer in Ctx.RenderData)
        {
            var ubo = new UniformBufferObject()
            {
                Model = Matrix4x4.Transpose(renderer.Logic.GetModelMatrix()),
                View = Matrix4x4.Transpose(Ctx.CameraViewMatrix),
                Proj = Matrix4x4.Transpose(Ctx.CameraProjectionMatrix),
                CamPos = Ctx.CameraTransform.Position.Value,
                TileCount = new Vector4(tileCountX, tileCountY, tileCountZ, 0),
                TileSize = tileSizeX,
                NearClip = Ctx.CameraData.NearClipPlane,
                FarClip = Ctx.CameraData.FarClipPlane,
                IsOrthographic = isOrtho ? 1u : 0u,
                TotalLightCount = (uint)activeLights.Length,
                DirectionalLightCount = directionalCount
            };

            var extraBytes = renderer.Material?.BuildCombinedUboData() ?? [];
            var materialParamsOffset = GetMaterialParamsOffset();
            var totalSize = materialParamsOffset + (ulong)extraBytes.Length;

            void* data;
            Ctx.Vk!.MapMemory(Ctx.Device, renderer.UniformBuffersMemory[currentImage], 0, totalSize, 0, &data);

            new Span<UniformBufferObject>(data, 1)[0] = ubo;

            if (extraBytes.Length > 0)
            {
                var dstPtr = (byte*)data + materialParamsOffset;
                fixed (byte* srcPtr = extraBytes)
                {
                    System.Buffer.MemoryCopy(srcPtr, dstPtr, extraBytes.Length, extraBytes.Length);
                }
            }

            Ctx.Vk!.UnmapMemory(Ctx.Device, renderer.UniformBuffersMemory[currentImage]);
        }
    }

    internal void CreateForScreen(Material screenMat)
    {
        Ctx.ScreenUniformBuffers = new Buffer[Ctx.Settings.MaxFramesInFlight];
        Ctx.ScreenUniformBuffersMemory = new DeviceMemory[Ctx.Settings.MaxFramesInFlight];

        var extraMaterialSize = (ulong)(screenMat.BuildScreenUboData(256).Length);
        var bufferSize = extraMaterialSize > 0 ? extraMaterialSize : 256;

        for (var i = 0; i < Ctx.Settings.MaxFramesInFlight; i++)
        {
            var (buffer, bufferMem) = Buffer.Create(Ctx, bufferSize, BufferUsageFlags.UniformBufferBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
            Ctx.ScreenUniformBuffers[i] = buffer;
            Ctx.ScreenUniformBuffersMemory[i] = bufferMem;
        }
    }

    internal void DestroyForScreen()
    {
        if (Ctx.ScreenUniformBuffers.Length == 0) return;

        for (var i = 0; i < Ctx.Settings.MaxFramesInFlight; i++)
        {
            Ctx.Vk!.DestroyBuffer(Ctx.Device, Ctx.ScreenUniformBuffers[i], null);
            Ctx.Vk!.FreeMemory(Ctx.Device, Ctx.ScreenUniformBuffersMemory[i], null);
        }

        Ctx.ScreenUniformBuffers = [];
        Ctx.ScreenUniformBuffersMemory = [];
    }

    internal void UpdateScreen(uint currentImage, Material screenMat)
    {
        var extraBytes = screenMat.BuildScreenUboData(256);
        if (extraBytes.Length == 0) return;

        void* data;
        Ctx.Vk!.MapMemory(Ctx.Device, Ctx.ScreenUniformBuffersMemory[currentImage], 0, (ulong)extraBytes.Length, 0,
            &data);

        fixed (byte* srcPtr = extraBytes)
        {
            System.Buffer.MemoryCopy(srcPtr, data, extraBytes.Length, extraBytes.Length);
        }

        Ctx.Vk!.UnmapMemory(Ctx.Device, Ctx.ScreenUniformBuffersMemory[currentImage]);
    }
}