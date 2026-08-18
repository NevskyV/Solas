using Silk.NET.Vulkan;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanDescriptorPool : VulkanInjectable
{
    private const uint MaxObjectsCapacity = 10000;

    internal void Create()
    {
        var frameCount = (uint)Ctx.Settings.MaxFramesInFlight;
        var globalSetCount = frameCount * 3u;
        var maxSets = frameCount * MaxObjectsCapacity + globalSetCount;

        DescriptorPoolSize[] poolSizes =
        [
            new()
            {
                Type = DescriptorType.UniformBuffer,
                DescriptorCount = maxSets * 2u + frameCount
            },
            new()
            {
                Type = DescriptorType.CombinedImageSampler,
                DescriptorCount = maxSets + frameCount * 2u
            },
            new()
            {
                Type = DescriptorType.StorageBuffer,
                DescriptorCount = frameCount * 10u
            }
        ];

        fixed (DescriptorPoolSize* poolSizesPointer = poolSizes)
        {
            var poolInfo = new DescriptorPoolCreateInfo
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                Flags = DescriptorPoolCreateFlags.FreeDescriptorSetBit,
                MaxSets = maxSets,
                PoolSizeCount = (uint)poolSizes.Length,
                PPoolSizes = poolSizesPointer
            };

            if (Ctx.Vk!.CreateDescriptorPool(Ctx.Device, &poolInfo, null, out Ctx.DescriptorPool) != Result.Success)
            {
                throw new InvalidOperationException("Failed to create the Vulkan descriptor pool.");
            }
        }
    }
}