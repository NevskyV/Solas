using Silk.NET.Vulkan;
using Solas.Render.Components;
using Solas.Render.Vulkan.Extensions;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanDescriptorSets : VulkanInjectable
{
    internal void CreateForObject(VulkanRenderData data)
    {
        var layouts = new DescriptorSetLayout[Ctx.Settings.MaxFramesInFlight];
        Array.Fill(layouts, Ctx.DescriptorSetLayout);

        fixed (DescriptorSetLayout* pLayouts = layouts)
        {
            DescriptorSetAllocateInfo allocInfo = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = Ctx.DescriptorPool,
                DescriptorSetCount = Ctx.Settings.MaxFramesInFlight,
                PSetLayouts = pLayouts,
            };

            data.DescriptorSets = new DescriptorSet[Ctx.Settings.MaxFramesInFlight];
            fixed (DescriptorSet* descriptorSetsPtr = data.DescriptorSets)
            {
                if (Ctx.Vk!.AllocateDescriptorSets(Ctx.Device, &allocInfo, descriptorSetsPtr) != Result.Success)
                {
                    throw new Exception("failed to allocate descriptor sets!");
                }
            }
        }

        for (var i = 0; i < Ctx.Settings.MaxFramesInFlight; i++)
        {
            DescriptorBufferInfo bufferInfo = new()
            {
                Buffer = data.UniformBuffers[i],
                Offset = 0,
                Range = (ulong)sizeof(UniformBufferObject)
            };

            DescriptorImageInfo imageInfo = new()
            {
                Sampler = data.GpuTexture.Sampler,
                ImageView = data.GpuTexture.ImageView,
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            };

            WriteDescriptorSet[] descriptorWrite =
            [
                new()
                {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = data.DescriptorSets[i],
                    DstBinding = 0,
                    DstArrayElement = 0,
                    DescriptorCount = 1,
                    DescriptorType = DescriptorType.UniformBuffer,
                    PBufferInfo = &bufferInfo,
                },
                new()
                {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = data.DescriptorSets[i],
                    DstBinding = 1,
                    DstArrayElement = 0,
                    DescriptorCount = 1,
                    DescriptorType = DescriptorType.CombinedImageSampler,
                    PImageInfo = &imageInfo,
                }
            ];

            Ctx.Vk!.UpdateDescriptorSets(Ctx.Device, descriptorWrite, []);
        }
    }

    internal void UpdateTextureBinding(VulkanRenderData data)
    {
        for (var i = 0; i < Ctx.Settings.MaxFramesInFlight; i++)
        {
            DescriptorImageInfo imageInfo = new()
            {
                Sampler = data.GpuTexture.Sampler,
                ImageView = data.GpuTexture.ImageView,
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            };

            WriteDescriptorSet descriptorWrite = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = data.DescriptorSets[i],
                DstBinding = 1,
                DstArrayElement = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                PImageInfo = &imageInfo,
            };

            Ctx.Vk!.UpdateDescriptorSets(Ctx.Device, 1, in descriptorWrite, 0, null);
        }
    }

    internal void FreeForObject(VulkanRenderData data)
    {
        if (data.DescriptorSets.Length == 0) return;

        fixed (DescriptorSet* pDescriptorSets = data.DescriptorSets)
        {
            Ctx.Vk!.FreeDescriptorSets(Ctx.Device, Ctx.DescriptorPool, Ctx.Settings.MaxFramesInFlight, pDescriptorSets);
        }

        data.DescriptorSets = null!;
    }
}