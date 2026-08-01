using Silk.NET.Vulkan;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanComputeDescriptorSetLayout : VulkanInjectable
{
    internal void Create()
    {
        DescriptorSetLayoutBinding ssboBinding = new()
        {
            Binding = 0,
            DescriptorType = DescriptorType.StorageBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.ComputeBit
        };

        DescriptorSetLayoutCreateInfo layoutInfo = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 1,
            PBindings = &ssboBinding
        };

        if (Ctx.Vk!.CreateDescriptorSetLayout(Ctx.Device, &layoutInfo, null, out Ctx.ComputeDescriptorSetLayout) !=
            Result.Success)
        {
            throw new Exception("failed to create compute descriptor set layout!");
        }
    }

    internal void Cleanup()
    {
        Ctx.Vk!.DestroyDescriptorSetLayout(Ctx.Device, Ctx.ComputeDescriptorSetLayout, null);
    }
}