using Silk.NET.Vulkan;

namespace Solas.Render.Backend.Vulkan;

internal unsafe class VulkanDescriptorSetLayout : VulkanInjectable
{
    internal void Create()
    {
        DescriptorSetLayoutBinding uboLayoutBinding = new DescriptorSetLayoutBinding()
        {
            Binding = 0,
            DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.VertexBit
        };

        DescriptorSetLayoutCreateInfo layoutInfo = new DescriptorSetLayoutCreateInfo()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 1,
            PBindings = &uboLayoutBinding
        };

        if (Ctx.Vk!.CreateDescriptorSetLayout(Ctx.Device, &layoutInfo, null, out Ctx.DescriptorSetLayout) !=
            Result.Success)
        {
            throw new Exception("failed to create descriptor set layout!");
        }
    }
}