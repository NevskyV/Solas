using Silk.NET.Vulkan;
using Solas.Render.Components;
using Solas.Render.Vulkan.Components;
using Solas.Render.Vulkan.Extensions;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanLightingResources : VulkanInjectable
{
    private const uint MaxLights = 1024;
    private const uint MaxTileIndices = 4 * 1024 * 1024;

    internal void Create()
    {
        uint lightsSize = (uint)(sizeof(LightGpu) * MaxLights);
        uint indicesSize = sizeof(uint) * MaxTileIndices;

        float tileSizeX = Ctx.Settings.TileSize.Z > 1 ? Ctx.Settings.TileSize.X * 4f : Ctx.Settings.TileSize.X;
        float tileSizeY = Ctx.Settings.TileSize.Z > 1 ? Ctx.Settings.TileSize.Y * 4f : Ctx.Settings.TileSize.Y;

        uint tileCountX = (uint)MathF.Ceiling(Ctx.RenderExtent.Width / tileSizeX);
        uint tileCountY = (uint)MathF.Ceiling(Ctx.RenderExtent.Height / tileSizeY);
        uint tileCountZ = (uint)Math.Max(1, (int)Ctx.Settings.TileSize.Z);
        uint gridSize = (uint)(sizeof(TileGridGpu) * tileCountX * tileCountY * tileCountZ);

        uint counterSize = sizeof(uint);
        uint frameParamsSize = (uint)sizeof(FrameParamsGpu);

        Ctx.LightBuffers = new Buffer[Ctx.Settings.MaxFramesInFlight];
        Ctx.LightBuffersMemory = new DeviceMemory[Ctx.Settings.MaxFramesInFlight];
        Ctx.LightBuffersMappedPointers = new void*[Ctx.Settings.MaxFramesInFlight];

        Ctx.GlobalLightIndicesBuffers = new Buffer[Ctx.Settings.MaxFramesInFlight];
        Ctx.GlobalLightIndicesBuffersMemory = new DeviceMemory[Ctx.Settings.MaxFramesInFlight];

        Ctx.TileGridBuffers = new Buffer[Ctx.Settings.MaxFramesInFlight];
        Ctx.TileGridBuffersMemory = new DeviceMemory[Ctx.Settings.MaxFramesInFlight];

        Ctx.GlobalIndexCounterBuffers = new Buffer[Ctx.Settings.MaxFramesInFlight];
        Ctx.GlobalIndexCounterBuffersMemory = new DeviceMemory[Ctx.Settings.MaxFramesInFlight];
        Ctx.GlobalIndexCounterMappedPointers = new void*[Ctx.Settings.MaxFramesInFlight];

        Ctx.FrameParamsBuffers = new Buffer[Ctx.Settings.MaxFramesInFlight];
        Ctx.FrameParamsBuffersMemory = new DeviceMemory[Ctx.Settings.MaxFramesInFlight];
        Ctx.FrameParamsMappedPointers = new void*[Ctx.Settings.MaxFramesInFlight];

        for (int i = 0; i < Ctx.Settings.MaxFramesInFlight; i++)
        {
            var (lightBuf, lightMem) = Buffer.Create(Ctx, lightsSize, BufferUsageFlags.StorageBufferBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
            Ctx.LightBuffers[i] = lightBuf;
            Ctx.LightBuffersMemory[i] = lightMem;
            void* pLight;
            Ctx.Vk!.MapMemory(Ctx.Device, lightMem, 0, lightsSize, 0, &pLight);
            Ctx.LightBuffersMappedPointers[i] = pLight;

            var (indicesBuf, indicesMem) = Buffer.Create(Ctx, indicesSize, BufferUsageFlags.StorageBufferBit,
                MemoryPropertyFlags.DeviceLocalBit);
            Ctx.GlobalLightIndicesBuffers[i] = indicesBuf;
            Ctx.GlobalLightIndicesBuffersMemory[i] = indicesMem;

            var (gridBuf, gridMem) = Buffer.Create(Ctx, gridSize, BufferUsageFlags.StorageBufferBit,
                MemoryPropertyFlags.DeviceLocalBit);
            Ctx.TileGridBuffers[i] = gridBuf;
            Ctx.TileGridBuffersMemory[i] = gridMem;

            var (counterBuf, counterMem) = Buffer.Create(Ctx, counterSize, BufferUsageFlags.StorageBufferBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
            Ctx.GlobalIndexCounterBuffers[i] = counterBuf;
            Ctx.GlobalIndexCounterBuffersMemory[i] = counterMem;
            void* pCounter;
            Ctx.Vk!.MapMemory(Ctx.Device, counterMem, 0, counterSize, 0, &pCounter);
            Ctx.GlobalIndexCounterMappedPointers[i] = pCounter;

            var (frameBuf, frameMem) = Buffer.Create(Ctx, frameParamsSize, BufferUsageFlags.UniformBufferBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
            Ctx.FrameParamsBuffers[i] = frameBuf;
            Ctx.FrameParamsBuffersMemory[i] = frameMem;
            void* pFrame;
            Ctx.Vk!.MapMemory(Ctx.Device, frameMem, 0, frameParamsSize, 0, &pFrame);
            Ctx.FrameParamsMappedPointers[i] = pFrame;
        }
    }

    internal void RecreateLightingSwapChainResources()
    {
        Ctx.Vk!.DeviceWaitIdle(Ctx.Device);

        for (int i = 0; i < Ctx.TileGridBuffers.Length; i++)
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

        float tileSizeX = Ctx.Settings.TileSize.Z > 1 ? Ctx.Settings.TileSize.X * 4f : Ctx.Settings.TileSize.X;
        float tileSizeY = Ctx.Settings.TileSize.Z > 1 ? Ctx.Settings.TileSize.Y * 4f : Ctx.Settings.TileSize.Y;

        uint tileCountX = (uint)MathF.Ceiling(Ctx.RenderExtent.Width / tileSizeX);
        uint tileCountY = (uint)MathF.Ceiling(Ctx.RenderExtent.Height / tileSizeY);
        uint tileCountZ = (uint)Math.Max(1, (int)Ctx.Settings.TileSize.Z);
        uint gridSize = (uint)(sizeof(TileGridGpu) * tileCountX * tileCountY * tileCountZ);

        for (int i = 0; i < Ctx.Settings.MaxFramesInFlight; i++)
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