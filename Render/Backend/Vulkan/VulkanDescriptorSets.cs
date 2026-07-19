using Silk.NET.Vulkan;
using Solas.Render.Components;

namespace Solas.Render.Backend.Vulkan;

internal unsafe class VulkanDescriptorSets : VulkanInjectable
{
    internal void Create()
    {
        var layouts = new DescriptorSetLayout[Ctx.MaxFramesInFlight];
        Array.Fill(layouts, Ctx.DescriptorSetLayout);
        fixed (DescriptorSetLayout* pLayouts = layouts)
        {
            DescriptorSetAllocateInfo allocInfo = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = Ctx.DescriptorPool,
                DescriptorSetCount = Ctx.MaxFramesInFlight,
                PSetLayouts = pLayouts,
            };

            Ctx.DescriptorSets = new DescriptorSet[Ctx.MaxFramesInFlight];
            fixed (DescriptorSet* descriptorSetsPtr = Ctx.DescriptorSets)
            {
                if (Ctx.Vk!.AllocateDescriptorSets(Ctx.Device, &allocInfo, descriptorSetsPtr) != Result.Success)
                {
                    throw new Exception("failed to allocate descriptor sets!");
                }
            }
        }

        for (var i = 0; i < Ctx.MaxFramesInFlight; i++)
        {
            DescriptorBufferInfo bufferInfo = new()
            {
                Buffer = Ctx.UniformBuffers![i],
                Offset = 0,
                Range = (ulong)sizeof(UniformBufferObject)
            };

            WriteDescriptorSet descriptorWrite = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = Ctx.DescriptorSets[i],
                DstBinding = 0,
                DstArrayElement = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.UniformBuffer,
                PBufferInfo = &bufferInfo,
            };

            Ctx.Vk.UpdateDescriptorSets(Ctx.Device, 1, &descriptorWrite, []);
        }
    }
}