using Silk.NET.Vulkan;

namespace Solas.Render.Backend.Vulkan;

internal unsafe class VulkanDescriptorPool : VulkanInjectable
{
    internal void Create()
    {
        DescriptorPoolSize poolSize = new DescriptorPoolSize()
        {
            Type = DescriptorType.UniformBuffer,
            DescriptorCount = Ctx.MaxFramesInFlight,
        };

        DescriptorPoolCreateInfo poolInfo = new DescriptorPoolCreateInfo()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            Flags = DescriptorPoolCreateFlags.FreeDescriptorSetBit,
            MaxSets = Ctx.MaxFramesInFlight,
            PoolSizeCount = 1,
            PPoolSizes = &poolSize
        };

        Ctx.Vk!.CreateDescriptorPool(Ctx.Device, &poolInfo, null, out Ctx.DescriptorPool);
    }
}