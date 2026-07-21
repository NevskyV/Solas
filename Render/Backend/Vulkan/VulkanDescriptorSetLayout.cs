using Silk.NET.Vulkan;

namespace Solas.Render.Vulkan;

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

        DescriptorSetLayoutBinding combinedImageSamplerLayoutBinding = new DescriptorSetLayoutBinding()
        {
            Binding = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit
        };

        var bindings = new[] { uboLayoutBinding, combinedImageSamplerLayoutBinding };

        fixed (DescriptorSetLayoutBinding* pBindings = bindings)
        {
            DescriptorSetLayoutCreateInfo layoutInfo = new DescriptorSetLayoutCreateInfo()
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = (uint)bindings.Length,
                PBindings = pBindings
            };

            if (Ctx.Vk!.CreateDescriptorSetLayout(Ctx.Device, &layoutInfo, null, out Ctx.DescriptorSetLayout) !=
                Result.Success)
            {
                throw new Exception("failed to create descriptor set layout!");
            }
        }
    }
}