using System;
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

        var extraMaterialSize = (ulong)(data.Material?.BuildCombinedUboData().Length ?? 0);

        for (var i = 0; i < Ctx.Settings.MaxFramesInFlight; i++)
        {
            DescriptorBufferInfo bufferInfo = new()
            {
                Buffer = data.UniformBuffers![i],
                Offset = 0,
                Range = (ulong)sizeof(UniformBufferObject) + extraMaterialSize
            };

            DescriptorImageInfo imageInfo = new()
            {
                Sampler = data.GpuTexture!.Sampler,
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
                },
                new()
                {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = data.DescriptorSets[i],
                    DstBinding = 2,
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
                Sampler = data.GpuTexture!.Sampler,
                ImageView = data.GpuTexture.ImageView,
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            };

            WriteDescriptorSet[] descriptorWrite =
            [
                new()
                {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = data.DescriptorSets[i],
                    DstBinding = 1,
                    DstArrayElement = 0,
                    DescriptorCount = 1,
                    DescriptorType = DescriptorType.CombinedImageSampler,
                    PImageInfo = &imageInfo,
                },
                new()
                {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = data.DescriptorSets[i],
                    DstBinding = 2,
                    DstArrayElement = 0,
                    DescriptorCount = 1,
                    DescriptorType = DescriptorType.CombinedImageSampler,
                    PImageInfo = &imageInfo,
                }
            ];

            Ctx.Vk!.UpdateDescriptorSets(Ctx.Device, descriptorWrite, []);
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

    internal void CreateForScreen(Material screenMat, ImageView inputView, Sampler sampler,
        Silk.NET.Vulkan.Buffer[] uniformBuffers)
    {
        int passCount = screenMat.PassCount;
        Ctx.ScreenDescriptorSets = new DescriptorSet[passCount][];

        var extraMaterialSize = (ulong)(screenMat.BuildCombinedUboData().Length);

        for (int p = 0; p < passCount; p++)
        {
            Ctx.ScreenDescriptorSets[p] = new DescriptorSet[Ctx.Settings.MaxFramesInFlight];

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

                fixed (DescriptorSet* descriptorSetsPtr = Ctx.ScreenDescriptorSets[p])
                {
                    if (Ctx.Vk!.AllocateDescriptorSets(Ctx.Device, &allocInfo, descriptorSetsPtr) != Result.Success)
                    {
                        throw new Exception("failed to allocate screen descriptor sets!");
                    }
                }
            }

            ImageView passInputView;
            if (p == 0)
            {
                passInputView = inputView;
            }
            else if (p % 2 == 1)
            {
                passInputView = Ctx.ScreenPingImageView;
            }
            else
            {
                passInputView = Ctx.ScreenPongImageView;
            }

            for (var i = 0; i < Ctx.Settings.MaxFramesInFlight; i++)
            {
                DescriptorBufferInfo bufferInfo = new()
                {
                    Buffer = uniformBuffers[i],
                    Offset = 0,
                    Range = extraMaterialSize > 0 ? extraMaterialSize : 16
                };

                DescriptorImageInfo imageInfo = new()
                {
                    Sampler = sampler,
                    ImageView = passInputView,
                    ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                };

                DescriptorImageInfo sceneImageInfo = new()
                {
                    Sampler = sampler,
                    ImageView = inputView,
                    ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                };

                WriteDescriptorSet[] descriptorWrite =
                [
                    new()
                    {
                        SType = StructureType.WriteDescriptorSet,
                        DstSet = Ctx.ScreenDescriptorSets[p][i],
                        DstBinding = 0,
                        DstArrayElement = 0,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.UniformBuffer,
                        PBufferInfo = &bufferInfo,
                    },
                    new()
                    {
                        SType = StructureType.WriteDescriptorSet,
                        DstSet = Ctx.ScreenDescriptorSets[p][i],
                        DstBinding = 1,
                        DstArrayElement = 0,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        PImageInfo = &imageInfo,
                    },
                    new()
                    {
                        SType = StructureType.WriteDescriptorSet,
                        DstSet = Ctx.ScreenDescriptorSets[p][i],
                        DstBinding = 2,
                        DstArrayElement = 0,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        PImageInfo = &sceneImageInfo,
                    }
                ];

                Ctx.Vk!.UpdateDescriptorSets(Ctx.Device, descriptorWrite, []);
            }
        }
    }

    internal void FreeForScreen()
    {
        if (Ctx.ScreenDescriptorSets == null || Ctx.ScreenDescriptorSets.Length == 0) return;

        for (int p = 0; p < Ctx.ScreenDescriptorSets.Length; p++)
        {
            if (Ctx.ScreenDescriptorSets[p] != null && Ctx.ScreenDescriptorSets[p].Length > 0)
            {
                fixed (DescriptorSet* pDescriptorSets = Ctx.ScreenDescriptorSets[p])
                {
                    Ctx.Vk!.FreeDescriptorSets(Ctx.Device, Ctx.DescriptorPool, Ctx.Settings.MaxFramesInFlight, pDescriptorSets);
                }
            }
        }

        Ctx.ScreenDescriptorSets = [];
    }
}