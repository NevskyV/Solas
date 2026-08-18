using Silk.NET.Vulkan;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanDescriptorSetLayout : VulkanInjectable
{
    internal void Create()
    {
        DescriptorSetLayoutBinding uboLayoutBinding = new()
        {
            Binding = 0,
            DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
            PImmutableSamplers = null
        };

        var bindingsList = new List<DescriptorSetLayoutBinding> { uboLayoutBinding };

        for (uint b = 1; b <= 8; b++)
        {
            bindingsList.Add(new DescriptorSetLayoutBinding
            {
                Binding = b,
                DescriptorType = DescriptorType.CombinedImageSampler,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.FragmentBit,
                PImmutableSamplers = null
            });
        }

        bindingsList.Add(new DescriptorSetLayoutBinding
        {
            Binding = 9,
            DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
            PImmutableSamplers = null
        });

        DescriptorSetLayoutBinding[] bindings = [.. bindingsList];

        fixed (DescriptorSetLayoutBinding* pBindings = bindings)
        {
            DescriptorSetLayoutCreateInfo layoutInfo = new()
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