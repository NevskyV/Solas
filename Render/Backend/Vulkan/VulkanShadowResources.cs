using Silk.NET.Vulkan;
using Solas.Render.Components;
using Solas.Render.Vulkan.Extensions;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanShadowResources : VulkanInjectable, IDisposable
{
    private const Format ShadowDepthFormat = Format.D32Sfloat;

    internal void Create()
    {
        var resolution = Math.Max(1u, Ctx.Settings.ShadowMapResolution);
        var cascadeCount = Math.Max(1u, Ctx.Settings.ShadowCascadeCount);
        var shadowedLightCapacity = GetSafeShadowedLightCapacity(Math.Max(1u, Ctx.Settings.MaxShadowedLights));
        Ctx.Settings.MaxShadowedLights = shadowedLightCapacity;
        var depthLayerCount = checked(cascadeCount + shadowedLightCapacity);
        var cubeLayerCount = checked(shadowedLightCapacity * 6u);
        var shadowImageUsage = ImageUsageFlags.DepthStencilAttachmentBit | ImageUsageFlags.SampledBit;

        (Ctx.ShadowDepthArrayImage, Ctx.ShadowDepthArrayMemory) = Image.Create(
            Ctx,
            resolution,
            resolution,
            1,
            depthLayerCount,
            SampleCountFlags.Count1Bit,
            ShadowDepthFormat,
            ImageTiling.Optimal,
            shadowImageUsage,
            MemoryPropertyFlags.DeviceLocalBit,
            ImageCreateFlags.None);

        Ctx.ShadowDepthArrayView = ImageView.Create(
            Ctx,
            Ctx.ShadowDepthArrayImage,
            ShadowDepthFormat,
            ImageAspectFlags.DepthBit,
            1,
            ImageViewType.Type2DArray,
            0,
            depthLayerCount);

        Ctx.ShadowDepthLayerViews = new ImageView[depthLayerCount];
        for (var layer = 0u; layer < depthLayerCount; layer++)
        {
            Ctx.ShadowDepthLayerViews[layer] = ImageView.Create(
                Ctx,
                Ctx.ShadowDepthArrayImage,
                ShadowDepthFormat,
                ImageAspectFlags.DepthBit,
                1,
                ImageViewType.Type2D,
                layer,
                1);
        }

        (Ctx.PointShadowCubeArrayImage, Ctx.PointShadowCubeArrayMemory) = Image.Create(
            Ctx,
            resolution,
            resolution,
            1,
            cubeLayerCount,
            SampleCountFlags.Count1Bit,
            ShadowDepthFormat,
            ImageTiling.Optimal,
            shadowImageUsage,
            MemoryPropertyFlags.DeviceLocalBit,
            ImageCreateFlags.CreateCubeCompatibleBit);

        Ctx.PointShadowCubeArrayView = ImageView.Create(
            Ctx,
            Ctx.PointShadowCubeArrayImage,
            ShadowDepthFormat,
            ImageAspectFlags.DepthBit,
            1,
            ImageViewType.TypeCubeArray,
            0,
            cubeLayerCount);

        Ctx.PointShadowCubeViews = new ImageView[shadowedLightCapacity];
        Ctx.PointShadowFaceViews = new ImageView[cubeLayerCount];
        for (var shadowIndex = 0u; shadowIndex < shadowedLightCapacity; shadowIndex++)
        {
            var baseLayer = shadowIndex * 6u;
            Ctx.PointShadowCubeViews[shadowIndex] = ImageView.Create(
                Ctx,
                Ctx.PointShadowCubeArrayImage,
                ShadowDepthFormat,
                ImageAspectFlags.DepthBit,
                1,
                ImageViewType.TypeCube,
                baseLayer,
                6);

            for (var face = 0u; face < 6u; face++)
            {
                Ctx.PointShadowFaceViews[baseLayer + face] = ImageView.Create(
                    Ctx,
                    Ctx.PointShadowCubeArrayImage,
                    ShadowDepthFormat,
                    ImageAspectFlags.DepthBit,
                    1,
                    ImageViewType.Type2D,
                    baseLayer + face,
                    1);
            }
        }

        var samplerInfo = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            MipmapMode = SamplerMipmapMode.Nearest,
            AddressModeU = SamplerAddressMode.ClampToBorder,
            AddressModeV = SamplerAddressMode.ClampToBorder,
            AddressModeW = SamplerAddressMode.ClampToBorder,
            MipLodBias = 0.0f,
            AnisotropyEnable = false,
            CompareEnable = true,
            CompareOp = CompareOp.LessOrEqual,
            MinLod = 0.0f,
            MaxLod = 0.0f,
            BorderColor = BorderColor.FloatOpaqueWhite,
            UnnormalizedCoordinates = false
        };

        if (Ctx.Vk!.CreateSampler(Ctx.Device, in samplerInfo, null, out Ctx.ShadowSampler) != Result.Success)
        {
            throw new InvalidOperationException("Failed to create the shadow comparison sampler.");
        }

        EnsureShadowMatrixCapacity(checked(cascadeCount + shadowedLightCapacity * 6u));
    }

    internal uint GetShadowedLightCapacityLimit()
    {
        var safeCapacity = GetSafeShadowedLightCapacity(uint.MaxValue);
        return Math.Min(safeCapacity, Math.Max(Ctx.Settings.MaxShadowedLights, 1u));
    }

    internal void EnsureShadowMapCapacity(uint requiredDepthLayers, uint requiredCubeMaps)
    {
        var hasEnoughDepthLayers = Ctx.ShadowDepthLayerViews.Length >= requiredDepthLayers;
        var hasEnoughCubeMaps = Ctx.PointShadowCubeViews.Length >= requiredCubeMaps;
        if (hasEnoughDepthLayers && hasEnoughCubeMaps)
        {
            return;
        }

        var cascadeCount = Math.Max(Ctx.Settings.ShadowCascadeCount, 1u);
        var additionalDepthLayers = requiredDepthLayers > cascadeCount
            ? requiredDepthLayers - cascadeCount
            : 0u;
        var requiredLightCapacity = Math.Max(requiredCubeMaps, additionalDepthLayers);
        var safeLightCapacity = GetSafeShadowedLightCapacity(uint.MaxValue);
        if (requiredLightCapacity > safeLightCapacity)
        {
            throw new InvalidOperationException(
                $"The current GPU shadow-memory budget supports {safeLightCapacity} shadowed lights at " +
                $"{Ctx.Settings.ShadowMapResolution}x{Ctx.Settings.ShadowMapResolution}, but {requiredLightCapacity} are active. " +
                "Reduce ShadowMapResolution or reduce the number of shadow-casting lights.");
        }

        Ctx.Settings.MaxShadowedLights =
            Math.Min(NextPowerOfTwo(Math.Max(requiredLightCapacity, 1u)), safeLightCapacity);
        Recreate();
        if (Ctx.LightingGlobalSetsSet0.Length != 0)
        {
            Ctx.LightingDescriptors.UpdateShadowImageBindings();
        }
    }

    internal void EnsureShadowMatrixCapacity(uint requiredMatrixCount)
    {
        var requestedCapacity = Math.Max(1u, requiredMatrixCount);
        if (Ctx.ShadowMatrixCapacity >= requestedCapacity && Ctx.ShadowMatrixBuffers.Length != 0)
        {
            return;
        }

        Ctx.Vk!.DeviceWaitIdle(Ctx.Device);
        DestroyShadowMatrixBuffers();

        Ctx.ShadowMatrixCapacity = NextPowerOfTwo(requestedCapacity);
        Ctx.ShadowMatrixBuffers = new Buffer[Ctx.Settings.MaxFramesInFlight];
        Ctx.ShadowMatrixBuffersMemory = new DeviceMemory[Ctx.Settings.MaxFramesInFlight];
        var bufferSize = checked((uint)(sizeof(ShadowMatrixGpu) * Ctx.ShadowMatrixCapacity));

        for (var frame = 0; frame < Ctx.Settings.MaxFramesInFlight; frame++)
        {
            var (buffer, memory) = Buffer.Create(
                Ctx,
                bufferSize,
                BufferUsageFlags.StorageBufferBit,
                MemoryPropertyFlags.DeviceLocalBit);
            Ctx.ShadowMatrixBuffers[frame] = buffer;
            Ctx.ShadowMatrixBuffersMemory[frame] = memory;
        }

        if (Ctx.LightingGlobalSetsSet0.Length != 0)
        {
            Ctx.LightingDescriptors.UpdateShadowMatrixBindings();
        }
    }

    private uint GetSafeShadowedLightCapacity(uint requestedCapacity)
    {
        var resolution = Math.Max(1u, Ctx.Settings.ShadowMapResolution);
        var cascadeCount = Math.Max(1u, Ctx.Settings.ShadowCascadeCount);
        var memoryProperties = Ctx.Vk!.GetPhysicalDeviceMemoryProperties(Ctx.PhysicalDevice);
        ulong largestDeviceLocalHeap = 0;

        for (var heapIndex = 0; heapIndex < memoryProperties.MemoryHeapCount; heapIndex++)
        {
            var heap = memoryProperties.MemoryHeaps[heapIndex];
            if ((heap.Flags & MemoryHeapFlags.DeviceLocalBit) != 0)
            {
                largestDeviceLocalHeap = Math.Max(largestDeviceLocalHeap, heap.Size);
            }
        }

        if (largestDeviceLocalHeap == 0)
        {
            throw new InvalidOperationException("No device-local Vulkan memory heap is available for shadow maps.");
        }

        var bytesPerDepthLayer = checked((ulong)resolution * resolution * 4ul);
        var cascadeBytes = checked(bytesPerDepthLayer * cascadeCount);
        var bytesPerShadowedLight = checked(bytesPerDepthLayer * 7ul);
        var shadowBudget = largestDeviceLocalHeap / 8ul;

        if (shadowBudget <= cascadeBytes || shadowBudget - cascadeBytes < bytesPerShadowedLight)
        {
            throw new InvalidOperationException(
                $"ShadowMapResolution {resolution} requires more than the reserved device-local shadow-memory budget. " +
                "Reduce ShadowMapResolution before enabling shadow maps.");
        }

        var capacityByBudget = (shadowBudget - cascadeBytes) / bytesPerShadowedLight;
        var capacity = Math.Min((ulong)requestedCapacity, capacityByBudget);
        return checked((uint)Math.Max(capacity, 1ul));
    }

    internal void Recreate()
    {
        Ctx.Vk!.DeviceWaitIdle(Ctx.Device);
        Dispose();
        Create();
    }

    public void Dispose()
    {
        DestroyShadowMatrixBuffers();

        if (Ctx.ShadowSampler.Handle != 0)
        {
            Ctx.Vk!.DestroySampler(Ctx.Device, Ctx.ShadowSampler, null);
            Ctx.ShadowSampler = default;
        }

        DestroyImageViews(Ctx.PointShadowFaceViews);
        DestroyImageViews(Ctx.PointShadowCubeViews);
        DestroyImageViews(Ctx.ShadowDepthLayerViews);
        Ctx.PointShadowFaceViews = [];
        Ctx.PointShadowCubeViews = [];
        Ctx.ShadowDepthLayerViews = [];

        if (Ctx.PointShadowCubeArrayView.Handle != 0)
        {
            Ctx.Vk!.DestroyImageView(Ctx.Device, Ctx.PointShadowCubeArrayView, null);
            Ctx.PointShadowCubeArrayView = default;
        }

        if (Ctx.ShadowDepthArrayView.Handle != 0)
        {
            Ctx.Vk!.DestroyImageView(Ctx.Device, Ctx.ShadowDepthArrayView, null);
            Ctx.ShadowDepthArrayView = default;
        }

        if (Ctx.PointShadowCubeArrayImage.Handle != 0)
        {
            Ctx.Vk!.DestroyImage(Ctx.Device, Ctx.PointShadowCubeArrayImage, null);
            Ctx.Vk!.FreeMemory(Ctx.Device, Ctx.PointShadowCubeArrayMemory, null);
            Ctx.PointShadowCubeArrayImage = default;
            Ctx.PointShadowCubeArrayMemory = default;
        }

        if (Ctx.ShadowDepthArrayImage.Handle != 0)
        {
            Ctx.Vk!.DestroyImage(Ctx.Device, Ctx.ShadowDepthArrayImage, null);
            Ctx.Vk!.FreeMemory(Ctx.Device, Ctx.ShadowDepthArrayMemory, null);
            Ctx.ShadowDepthArrayImage = default;
            Ctx.ShadowDepthArrayMemory = default;
        }

        Ctx.ShadowImagesInitialized = false;
    }

    private void DestroyShadowMatrixBuffers()
    {
        for (var frame = 0; frame < Ctx.ShadowMatrixBuffers.Length; frame++)
        {
            if (Ctx.ShadowMatrixBuffers[frame].Handle != 0)
            {
                Ctx.Vk!.DestroyBuffer(Ctx.Device, Ctx.ShadowMatrixBuffers[frame], null);
            }

            if (Ctx.ShadowMatrixBuffersMemory[frame].Handle != 0)
            {
                Ctx.Vk!.FreeMemory(Ctx.Device, Ctx.ShadowMatrixBuffersMemory[frame], null);
            }
        }

        Ctx.ShadowMatrixBuffers = [];
        Ctx.ShadowMatrixBuffersMemory = [];
        Ctx.ShadowMatrixCapacity = 0;
    }

    private void DestroyImageViews(ImageView[] views)
    {
        for (var index = 0; index < views.Length; index++)
        {
            if (views[index].Handle != 0)
            {
                Ctx.Vk!.DestroyImageView(Ctx.Device, views[index], null);
            }
        }
    }

    private static uint NextPowerOfTwo(uint value)
    {
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }
}