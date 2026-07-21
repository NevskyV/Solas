using Silk.NET.Vulkan;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanDescriptorPool : VulkanInjectable
{
    internal void Create()
    {
        DescriptorPoolSize[] poolSize =
        [
            new()
            {
                Type = DescriptorType.UniformBuffer,
                DescriptorCount = Ctx.MaxFramesInFlight,
            },
            new()
            {
                Type = DescriptorType.CombinedImageSampler,
                DescriptorCount = Ctx.MaxFramesInFlight,
            }
        ];

        fixed (DescriptorPoolSize* pPoolSizes = poolSize)
        {
            DescriptorPoolCreateInfo poolInfo = new DescriptorPoolCreateInfo()
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                Flags = DescriptorPoolCreateFlags.FreeDescriptorSetBit,
                MaxSets = Ctx.MaxFramesInFlight,
                PoolSizeCount = (uint)poolSize.Length,
                PPoolSizes = pPoolSizes
            };

            Ctx.Vk!.CreateDescriptorPool(Ctx.Device, &poolInfo, null, out Ctx.DescriptorPool);
        }
    }
}