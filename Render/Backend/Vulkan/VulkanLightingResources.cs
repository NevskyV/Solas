using Silk.NET.Vulkan;
using Solas.Render.Vulkan.Components;
using Solas.Render.Vulkan.Extensions;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanLightingResources : VulkanInjectable
{
    private const uint MaxLights = 1024;
    private const uint MaxTileIndices = 1024 * 256;

    internal void Create()
    {
        uint lightsSize = (uint)(sizeof(PointLightGpu) * MaxLights);
        uint indicesSize = sizeof(uint) * MaxTileIndices;

        uint tileCountX = (uint)MathF.Ceiling(Ctx.SwapChainExtent.Width / 16.0f);
        uint tileCountY = (uint)MathF.Ceiling(Ctx.SwapChainExtent.Height / 16.0f);
        uint gridSize = (uint)(sizeof(TileGridGpu) * tileCountX * tileCountY);

        uint counterSize = sizeof(uint);
        uint frameParamsSize = (uint)sizeof(FrameParamsGpu);

        Ctx.LightBuffers = new Buffer[Ctx.MaxFramesInFlight];
        Ctx.LightBuffersMemory = new DeviceMemory[Ctx.MaxFramesInFlight];
        Ctx.LightBuffersMappedPointers = new void*[Ctx.MaxFramesInFlight];

        Ctx.GlobalLightIndicesBuffers = new Buffer[Ctx.MaxFramesInFlight];
        Ctx.GlobalLightIndicesBuffersMemory = new DeviceMemory[Ctx.MaxFramesInFlight];

        Ctx.TileGridBuffers = new Buffer[Ctx.MaxFramesInFlight];
        Ctx.TileGridBuffersMemory = new DeviceMemory[Ctx.MaxFramesInFlight];

        Ctx.GlobalIndexCounterBuffers = new Buffer[Ctx.MaxFramesInFlight];
        Ctx.GlobalIndexCounterBuffersMemory = new DeviceMemory[Ctx.MaxFramesInFlight];
        Ctx.GlobalIndexCounterMappedPointers = new void*[Ctx.MaxFramesInFlight];

        Ctx.FrameParamsBuffers = new Buffer[Ctx.MaxFramesInFlight];
        Ctx.FrameParamsBuffersMemory = new DeviceMemory[Ctx.MaxFramesInFlight];
        Ctx.FrameParamsMappedPointers = new void*[Ctx.MaxFramesInFlight];

        for (int i = 0; i < Ctx.MaxFramesInFlight; i++)
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

    internal void UpdateGpuLights(PointLightGpu[] activeLights)
    {
        uint frameIdx = Ctx.FrameIndex;
        void* mappedPtr = Ctx.LightBuffersMappedPointers[frameIdx];
        uint lightsSize = (uint)(sizeof(PointLightGpu) * activeLights.Length);

        fixed (PointLightGpu* pLights = activeLights)
        {
            System.Buffer.MemoryCopy(pLights, mappedPtr, lightsSize, lightsSize);
        }
    }

    internal unsafe void RecreateLightingSwapChainResources()
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

        uint tileCountX = (uint)MathF.Ceiling(Ctx.SwapChainExtent.Width / 16.0f);
        uint tileCountY = (uint)MathF.Ceiling(Ctx.SwapChainExtent.Height / 16.0f);
        uint gridSize = (uint)(sizeof(TileGridGpu) * tileCountX * tileCountY);

        for (int i = 0; i < Ctx.MaxFramesInFlight; i++)
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