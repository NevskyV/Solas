using Silk.NET.Vulkan;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanComputeDescriptorSets : VulkanInjectable
{
    internal void Create()
    {
        Ctx.ComputeDescriptorSets = new DescriptorSet[Ctx.MaxFramesInFlight];

        var layouts = new DescriptorSetLayout[Ctx.MaxFramesInFlight];
        Array.Fill(layouts, Ctx.ComputeDescriptorSetLayout);

        fixed (DescriptorSetLayout* pLayouts = layouts)
        fixed (DescriptorSet* pSets = Ctx.ComputeDescriptorSets)
        {
            DescriptorSetAllocateInfo allocInfo = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = Ctx.DescriptorPool,
                DescriptorSetCount = Ctx.MaxFramesInFlight,
                PSetLayouts = pLayouts
            };

            if (Ctx.Vk!.AllocateDescriptorSets(Ctx.Device, &allocInfo, pSets) != Result.Success)
            {
                throw new Exception("failed to allocate compute descriptor sets!");
            }
        }

        for (int i = 0; i < Ctx.MaxFramesInFlight; i++)
        {
            DescriptorBufferInfo ssboInfo = new()
            {
                Buffer = Ctx.ShaderStorageBuffers![i],
                Offset = 0,
                Range = Vk.WholeSize
            };

            WriteDescriptorSet write = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = Ctx.ComputeDescriptorSets[i],
                DstBinding = 0,
                DstArrayElement = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                PBufferInfo = &ssboInfo
            };

            Ctx.Vk!.UpdateDescriptorSets(Ctx.Device, 1, &write, 0, null);
        }
    }
}