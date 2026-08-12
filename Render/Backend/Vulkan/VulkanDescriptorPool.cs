using Silk.NET.Vulkan;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanDescriptorPool : VulkanInjectable
{
    private const uint MaxObjectsCapacity = 10000;

    internal void Create()
    {
        var maxSets = Ctx.Settings.MaxFramesInFlight * MaxObjectsCapacity;

        DescriptorPoolSize[] poolSizes =
        [
            new()
            {
                Type = DescriptorType.UniformBuffer,
                DescriptorCount = maxSets,
            },
            new()
            {
                Type = DescriptorType.CombinedImageSampler,
                DescriptorCount = maxSets,
            },
            new()
            {
                Type = DescriptorType.StorageBuffer,
                DescriptorCount = Ctx.Settings.MaxFramesInFlight * 2u
            }
        ];

        fixed (DescriptorPoolSize* pPoolSizes = poolSizes)
        {
            DescriptorPoolCreateInfo poolInfo = new()
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                Flags = DescriptorPoolCreateFlags.FreeDescriptorSetBit,
                MaxSets = maxSets,
                PoolSizeCount = (uint)poolSizes.Length,
                PPoolSizes = pPoolSizes
            };

            if (Ctx.Vk!.CreateDescriptorPool(Ctx.Device, &poolInfo, null, out Ctx.DescriptorPool) != Result.Success)
            {
                throw new Exception("failed to create descriptor pool!");
            }
        }
    }
}