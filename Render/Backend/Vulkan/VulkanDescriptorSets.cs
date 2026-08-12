using Silk.NET.Vulkan;
using Solas.Render.Components;
using Solas.Render.Vulkan.Components;
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

        UpdateTextureBinding(data);
    }

    internal void UpdateTextureBinding(VulkanRenderData data)
    {
        var extraMaterialSize = (ulong)(data.Material?.BuildCombinedUboData().Length ?? 0);
        data.AcquireGpuTextures(Ctx.ResourceManager);

        for (var i = 0; i < Ctx.Settings.MaxFramesInFlight; i++)
        {
            DescriptorBufferInfo bufferInfo = new()
            {
                Buffer = data.UniformBuffers![i],
                Offset = 0,
                Range = (ulong)sizeof(UniformBufferObject) + extraMaterialSize
            };

            List<WriteDescriptorSet> writes =
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
                }
            ];

            List<DescriptorImageInfo> imageInfos = new();

            for (uint b = 1; b <= 8; b++)
            {
                TextureGpu gpuTex;
                if (data.BoundGpuTextures.TryGetValue((int)b, out var customTex))
                {
                    gpuTex = customTex;
                }
                else
                {
                    gpuTex = Ctx.ResourceManager.AcquireDefaultTexture();
                }

                var imgInfo = new DescriptorImageInfo
                {
                    Sampler = gpuTex.Sampler,
                    ImageView = gpuTex.ImageView,
                    ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                };
                imageInfos.Add(imgInfo);
            }

            DescriptorImageInfo[] imgInfosArray = [.. imageInfos];
            fixed (DescriptorImageInfo* pImgInfos = imgInfosArray)
            {
                for (uint b = 1; b <= 8; b++)
                {
                    writes.Add(new WriteDescriptorSet
                    {
                        SType = StructureType.WriteDescriptorSet,
                        DstSet = data.DescriptorSets[i],
                        DstBinding = b,
                        DstArrayElement = 0,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        PImageInfo = &pImgInfos[b - 1],
                    });
                }

                WriteDescriptorSet[] writesArray = [.. writes];
                Ctx.Vk!.UpdateDescriptorSets(Ctx.Device, writesArray, []);
            }
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
        var passCount = screenMat.PassCount;
        Ctx.ScreenDescriptorSets = new DescriptorSet[passCount][];

        const uint alignment = 256;
        uint passOffset = 0;

        for (var p = 0; p < passCount; p++)
        {
            var passSize = (uint)screenMat.BuildPassUboData(p).Length;
            var paddedPassSize = (passSize + alignment - 1) & ~(alignment - 1);
            if (paddedPassSize == 0) paddedPassSize = alignment;

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
                    Offset = passOffset,
                    Range = paddedPassSize
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

                List<WriteDescriptorSet> writes =
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
                    }
                ];

                List<DescriptorImageInfo> imageInfos = new();
                for (uint b = 1; b <= 8; b++)
                {
                    var imgInfo = (b == 2) ? sceneImageInfo : imageInfo;
                    imageInfos.Add(imgInfo);
                }

                DescriptorImageInfo[] imgInfosArray = [.. imageInfos];
                fixed (DescriptorImageInfo* pImgInfos = imgInfosArray)
                {
                    for (uint b = 1; b <= 8; b++)
                    {
                        writes.Add(new WriteDescriptorSet
                        {
                            SType = StructureType.WriteDescriptorSet,
                            DstSet = Ctx.ScreenDescriptorSets[p][i],
                            DstBinding = b,
                            DstArrayElement = 0,
                            DescriptorCount = 1,
                            DescriptorType = DescriptorType.CombinedImageSampler,
                            PImageInfo = &pImgInfos[b - 1],
                        });
                    }

                    WriteDescriptorSet[] writesArray = [.. writes];
                    Ctx.Vk!.UpdateDescriptorSets(Ctx.Device, writesArray, []);
                }
            }

            passOffset += paddedPassSize;
        }
    }

    internal void FreeForScreen()
    {
        if (Ctx.ScreenDescriptorSets.Length == 0) return;

        for (var p = 0; p < Ctx.ScreenDescriptorSets.Length; p++)
        {
            if (Ctx.ScreenDescriptorSets[p].Length > 0)
            {
                fixed (DescriptorSet* pDescriptorSets = Ctx.ScreenDescriptorSets[p])
                {
                    Ctx.Vk!.FreeDescriptorSets(Ctx.Device, Ctx.DescriptorPool, Ctx.Settings.MaxFramesInFlight,
                        pDescriptorSets);
                }
            }
        }

        Ctx.ScreenDescriptorSets = [];
    }
}