using Silk.NET.Vulkan;
using Solas.Render.Components;
using Solas.Render.Vulkan.Extensions;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanLightingResources : VulkanInjectable
{
    private const uint MaxLights = 1024;
    private const uint MaxTileIndices = 4 * 1024 * 1024;

    internal void Create()
    {
        var lightsSize = (uint)(sizeof(LightGpu) * MaxLights);
        var indicesSize = sizeof(uint) * MaxTileIndices;

        var tileSizeX = Ctx.Settings.TileSize.Z > 1 ? Ctx.Settings.TileSize.X * 4f : Ctx.Settings.TileSize.X;
        var tileSizeY = Ctx.Settings.TileSize.Z > 1 ? Ctx.Settings.TileSize.Y * 4f : Ctx.Settings.TileSize.Y;

        var tileCountX = (uint)MathF.Ceiling(Ctx.RenderExtent.Width / tileSizeX);
        var tileCountY = (uint)MathF.Ceiling(Ctx.RenderExtent.Height / tileSizeY);
        var tileCountZ = (uint)Math.Max(1, (int)Ctx.Settings.TileSize.Z);
        var gridSize = (uint)(sizeof(TileGridGpu) * tileCountX * tileCountY * tileCountZ);

        uint counterSize = sizeof(uint);
        var frameParamsSize = (uint)sizeof(FrameParamsGpu);

        Ctx.LightBuffers = new Buffer[Ctx.Settings.MaxFramesInFlight];
        Ctx.LightBuffersMemory = new DeviceMemory[Ctx.Settings.MaxFramesInFlight];
        Ctx.LightUploadBuffers = new Buffer[Ctx.Settings.MaxFramesInFlight];
        Ctx.LightUploadBuffersMemory = new DeviceMemory[Ctx.Settings.MaxFramesInFlight];
        Ctx.LightUploadBuffersMappedPointers = new void*[Ctx.Settings.MaxFramesInFlight];

        Ctx.GlobalLightIndicesBuffers = new Buffer[Ctx.Settings.MaxFramesInFlight];
        Ctx.GlobalLightIndicesBuffersMemory = new DeviceMemory[Ctx.Settings.MaxFramesInFlight];

        Ctx.TileGridBuffers = new Buffer[Ctx.Settings.MaxFramesInFlight];
        Ctx.TileGridBuffersMemory = new DeviceMemory[Ctx.Settings.MaxFramesInFlight];

        Ctx.GlobalIndexCounterBuffers = new Buffer[Ctx.Settings.MaxFramesInFlight];
        Ctx.GlobalIndexCounterBuffersMemory = new DeviceMemory[Ctx.Settings.MaxFramesInFlight];

        Ctx.FrameParamsBuffers = new Buffer[Ctx.Settings.MaxFramesInFlight];
        Ctx.FrameParamsBuffersMemory = new DeviceMemory[Ctx.Settings.MaxFramesInFlight];
        Ctx.FrameParamsMappedPointers = new void*[Ctx.Settings.MaxFramesInFlight];

        Ctx.IndirectDrawBuffers = new Buffer[Ctx.Settings.MaxFramesInFlight];
        Ctx.IndirectDrawBuffersMemory = new DeviceMemory[Ctx.Settings.MaxFramesInFlight];
        Ctx.IndirectDrawMappedPointers = new void*[Ctx.Settings.MaxFramesInFlight];

        Ctx.ObjectDataBuffers = new Buffer[Ctx.Settings.MaxFramesInFlight];
        Ctx.ObjectDataBuffersMemory = new DeviceMemory[Ctx.Settings.MaxFramesInFlight];
        Ctx.ObjectDataMappedPointers = new void*[Ctx.Settings.MaxFramesInFlight];

        var indirectSize = (uint)(sizeof(DrawIndexedIndirectCommand) * 4096);
        var objectDataSize = (uint)(sizeof(ObjectDataGpu) * 4096);

        for (var i = 0; i < Ctx.Settings.MaxFramesInFlight; i++)
        {
            var (lightBuf, lightMem) = Buffer.Create(
                Ctx,
                lightsSize,
                BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit,
                MemoryPropertyFlags.DeviceLocalBit);
            Ctx.LightBuffers[i] = lightBuf;
            Ctx.LightBuffersMemory[i] = lightMem;
            var (lightUploadBuf, lightUploadMem) = Buffer.Create(
                Ctx,
                lightsSize,
                BufferUsageFlags.TransferSrcBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
            Ctx.LightUploadBuffers[i] = lightUploadBuf;
            Ctx.LightUploadBuffersMemory[i] = lightUploadMem;
            void* pLightUpload;
            Ctx.Vk!.MapMemory(Ctx.Device, lightUploadMem, 0, lightsSize, 0, &pLightUpload);
            Ctx.LightUploadBuffersMappedPointers[i] = pLightUpload;

            var (indicesBuf, indicesMem) = Buffer.Create(Ctx, indicesSize, BufferUsageFlags.StorageBufferBit,
                MemoryPropertyFlags.DeviceLocalBit);
            Ctx.GlobalLightIndicesBuffers[i] = indicesBuf;
            Ctx.GlobalLightIndicesBuffersMemory[i] = indicesMem;

            var (gridBuf, gridMem) = Buffer.Create(Ctx, gridSize, BufferUsageFlags.StorageBufferBit,
                MemoryPropertyFlags.DeviceLocalBit);
            Ctx.TileGridBuffers[i] = gridBuf;
            Ctx.TileGridBuffersMemory[i] = gridMem;

            var (counterBuf, counterMem) = Buffer.Create(Ctx, counterSize,
                BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit,
                MemoryPropertyFlags.DeviceLocalBit);
            Ctx.GlobalIndexCounterBuffers[i] = counterBuf;
            Ctx.GlobalIndexCounterBuffersMemory[i] = counterMem;

            var (frameBuf, frameMem) = Buffer.Create(Ctx, frameParamsSize, BufferUsageFlags.UniformBufferBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
            Ctx.FrameParamsBuffers[i] = frameBuf;
            Ctx.FrameParamsBuffersMemory[i] = frameMem;
            void* pFrame;
            Ctx.Vk!.MapMemory(Ctx.Device, frameMem, 0, frameParamsSize, 0, &pFrame);
            Ctx.FrameParamsMappedPointers[i] = pFrame;

            var (indirectBuf, indirectMem) = Buffer.Create(Ctx, indirectSize,
                BufferUsageFlags.IndirectBufferBit | BufferUsageFlags.StorageBufferBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
            Ctx.IndirectDrawBuffers[i] = indirectBuf;
            Ctx.IndirectDrawBuffersMemory[i] = indirectMem;
            void* pIndirect;
            Ctx.Vk!.MapMemory(Ctx.Device, indirectMem, 0, indirectSize, 0, &pIndirect);
            Ctx.IndirectDrawMappedPointers[i] = pIndirect;

            var (objectBuf, objectMem) = Buffer.Create(Ctx, objectDataSize, BufferUsageFlags.StorageBufferBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
            Ctx.ObjectDataBuffers[i] = objectBuf;
            Ctx.ObjectDataBuffersMemory[i] = objectMem;
            void* pObject;
            Ctx.Vk!.MapMemory(Ctx.Device, objectMem, 0, objectDataSize, 0, &pObject);
            Ctx.ObjectDataMappedPointers[i] = pObject;
        }
    }

    internal void RecreateLightingSwapChainResources()
    {
        Ctx.Vk!.DeviceWaitIdle(Ctx.Device);

        for (var i = 0; i < Ctx.TileGridBuffers.Length; i++)
        {
            if (Ctx.TileGridBuffers[i].Handle != 0)
            {
                Ctx.Vk!.DestroyBuffer(Ctx.Device, Ctx.TileGridBuffers[i], null);
                Ctx.TileGridBuffers[i] = default;
            }

            if (Ctx.TileGridBuffersMemory[i].Handle != 0)
            {
                Ctx.Vk!.FreeMemory(Ctx.Device, Ctx.TileGridBuffersMemory[i], null);
                Ctx.TileGridBuffersMemory[i] = default;
            }
        }

        var tileSizeX = Ctx.Settings.TileSize.Z > 1 ? Ctx.Settings.TileSize.X * 4f : Ctx.Settings.TileSize.X;
        var tileSizeY = Ctx.Settings.TileSize.Z > 1 ? Ctx.Settings.TileSize.Y * 4f : Ctx.Settings.TileSize.Y;

        var tileCountX = (uint)MathF.Ceiling(Ctx.RenderExtent.Width / tileSizeX);
        var tileCountY = (uint)MathF.Ceiling(Ctx.RenderExtent.Height / tileSizeY);
        var tileCountZ = (uint)Math.Max(1, (int)Ctx.Settings.TileSize.Z);
        var gridSize = (uint)(sizeof(TileGridGpu) * tileCountX * tileCountY * tileCountZ);

        for (var i = 0; i < Ctx.Settings.MaxFramesInFlight; i++)
        {
            var (gridBuf, gridMem) = Buffer.Create(
                Ctx,
                gridSize,
                BufferUsageFlags.StorageBufferBit,
                MemoryPropertyFlags.DeviceLocalBit
            );

            Ctx.TileGridBuffers[i] = gridBuf;
            Ctx.TileGridBuffersMemory[i] = gridMem;

            DescriptorBufferInfo infoGrid = new()
            {
                Buffer = Ctx.TileGridBuffers[i],
                Offset = 0,
                Range = Vk.WholeSize
            };

            WriteDescriptorSet writeGrid = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = Ctx.LightingGlobalSetsSet0[i],
                DstBinding = 2,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                PBufferInfo = &infoGrid
            };

            Ctx.Vk!.UpdateDescriptorSets(Ctx.Device, 1, &writeGrid, 0, null);
        }
    }
}