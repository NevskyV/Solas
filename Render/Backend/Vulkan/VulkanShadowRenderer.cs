using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Solas.Render.Components;
using Solas.Render.Vulkan.Extensions;
using Buffer = Silk.NET.Vulkan.Buffer;
using ShaderModule = Silk.NET.Vulkan.ShaderModule;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanShadowRenderer : VulkanInjectable, IDisposable
{
    private ulong _lastShadowContentHash = ulong.MaxValue;

    internal void Create()
    {
        CreateComputePipelines();
        CreateDepthPipeline();
    }

    internal bool Record(CommandBuffer commandBuffer, IReadOnlyList<int> renderObjectIndices, uint shadowMatrixCount,
        ReadOnlySpan<LightGpu> lights, ulong shadowContentHash)
    {
        if (shadowMatrixCount == 0 || renderObjectIndices.Count == 0)
        {
            return false;
        }

        var shouldRenderShadowMaps = !Ctx.ShadowImagesInitialized || shadowContentHash != _lastShadowContentHash;
        if (shouldRenderShadowMaps)
        {
            TransitionShadowImages(
                commandBuffer,
                Ctx.ShadowImagesInitialized ? ImageLayout.ShaderReadOnlyOptimal : ImageLayout.Undefined,
                ImageLayout.DepthStencilAttachmentOptimal,
                Ctx.ShadowImagesInitialized ? AccessFlags2.ShaderReadBit : AccessFlags2.None,
                AccessFlags2.DepthStencilAttachmentWriteBit,
                Ctx.ShadowImagesInitialized ? PipelineStageFlags2.FragmentShaderBit : PipelineStageFlags2.TopOfPipeBit,
                PipelineStageFlags2.EarlyFragmentTestsBit | PipelineStageFlags2.LateFragmentTestsBit);
        }

        Ctx.Vk!.CmdBindPipeline(commandBuffer, PipelineBindPoint.Compute, Ctx.ShadowSetupPipeline);
        DescriptorSet[] setupSets =
            [Ctx.LightingGlobalSetsSet0[Ctx.FrameIndex], Ctx.LightingFrameSetsSet1[Ctx.FrameIndex]];
        fixed (DescriptorSet* setupSetsPointer = setupSets)
        {
            Ctx.Vk.CmdBindDescriptorSets(
                commandBuffer,
                PipelineBindPoint.Compute,
                Ctx.ShadowSetupPipelineLayout,
                0,
                (uint)setupSets.Length,
                setupSetsPointer,
                0,
                null);
        }

        Ctx.Vk.CmdDispatch(commandBuffer, (uint)((lights.Length + 63) / 64), 1, 1);
        BarrierComputeWritesToShadowConsumers(commandBuffer);

        if (!shouldRenderShadowMaps)
        {
            return false;
        }

        uint shadowRecordIndex = 0;
        uint depthLayerIndex = 0;
        for (var lightIndex = 0; lightIndex < lights.Length; lightIndex++)
        {
            var light = lights[lightIndex];
            if (light.ShadowParams.X < 0.0f)
            {
                continue;
            }

            var lightType = (uint)light.PositionOrDirection.W;
            if (lightType == 2u)
            {
                var cascadeCount = Math.Min(Math.Max(Ctx.Settings.ShadowCascadeCount, 1u), 6u);
                for (var cascade = 0u; cascade < cascadeCount; cascade++)
                {
                    RenderShadowRecord(commandBuffer, renderObjectIndices, shadowRecordIndex,
                        Ctx.ShadowDepthLayerViews[depthLayerIndex]);
                    shadowRecordIndex++;
                    depthLayerIndex++;
                }
            }
            else if (lightType == 1u)
            {
                RenderShadowRecord(commandBuffer, renderObjectIndices, shadowRecordIndex,
                    Ctx.ShadowDepthLayerViews[depthLayerIndex]);
                shadowRecordIndex++;
                depthLayerIndex++;
            }
            else
            {
                var cubeIndex = (uint)Math.Max(light.Extra1.Z, 0.0f);
                var firstFace = cubeIndex * 6u;
                for (var face = 0u; face < 6u; face++)
                {
                    RenderShadowRecord(commandBuffer, renderObjectIndices, shadowRecordIndex,
                        Ctx.PointShadowFaceViews[firstFace + face]);
                    shadowRecordIndex++;
                }
            }
        }

        TransitionShadowImages(
            commandBuffer,
            ImageLayout.DepthStencilAttachmentOptimal,
            ImageLayout.ShaderReadOnlyOptimal,
            AccessFlags2.DepthStencilAttachmentWriteBit,
            AccessFlags2.ShaderReadBit,
            PipelineStageFlags2.EarlyFragmentTestsBit | PipelineStageFlags2.LateFragmentTestsBit,
            PipelineStageFlags2.FragmentShaderBit);
        Ctx.ShadowImagesInitialized = true;
        _lastShadowContentHash = shadowContentHash;
        return true;
    }

    public void Dispose()
    {
        DestroyPipeline(ref Ctx.ShadowDepthRigidPipeline);
        DestroyPipelineLayout(ref Ctx.ShadowDepthRigidPipelineLayout);
        DestroyPipeline(ref Ctx.ShadowSetupPipeline);
        DestroyPipelineLayout(ref Ctx.ShadowSetupPipelineLayout);
        _lastShadowContentHash = ulong.MaxValue;
    }

    private void CreateComputePipelines()
    {
        var setupCode = LoadEmbeddedShader("ShadowSetup.spv");
        Ctx.ShadowSetupPipeline = CreateComputePipeline(
            setupCode,
            [Ctx.LightingGlobalSet0Layout, Ctx.LightingFrameSet1Layout],
            out Ctx.ShadowSetupPipelineLayout);
    }

    private void CreateDepthPipeline()
    {
        var shaderCode = LoadEmbeddedShader("ShadowDepthRigid.spv");
        var shaderModule = CreateShaderModule(shaderCode);
        var vertexStage = new PipelineShaderStageCreateInfo
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = shaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        };
        DescriptorSetLayout[] setLayouts = [Ctx.LightingGlobalSet0Layout, Ctx.DescriptorSetLayout];
        var vertexBinding = Vertex.GetBindingDescription();
        var vertexAttributes = Vertex.GetAttributeDescriptions();
        DynamicState[] dynamicStates = [DynamicState.Viewport, DynamicState.Scissor];
        PushConstantRange pushConstantRange = new()
        {
            StageFlags = ShaderStageFlags.VertexBit,
            Offset = 0,
            Size = sizeof(uint)
        };

        fixed (DescriptorSetLayout* setLayoutsPointer = setLayouts)
        fixed (VertexInputAttributeDescription* vertexAttributesPointer = vertexAttributes)
        fixed (DynamicState* dynamicStatesPointer = dynamicStates)
        {
            var layoutInfo = new PipelineLayoutCreateInfo
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = (uint)setLayouts.Length,
                PSetLayouts = setLayoutsPointer,
                PushConstantRangeCount = 1,
                PPushConstantRanges = &pushConstantRange
            };
            if (Ctx.Vk!.CreatePipelineLayout(Ctx.Device, &layoutInfo, null, out Ctx.ShadowDepthRigidPipelineLayout) !=
                Result.Success)
            {
                throw new InvalidOperationException("Failed to create the shadow depth pipeline layout.");
            }

            var vertexInput = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1,
                PVertexBindingDescriptions = &vertexBinding,
                VertexAttributeDescriptionCount = (uint)vertexAttributes.Length,
                PVertexAttributeDescriptions = vertexAttributesPointer
            };
            var inputAssembly = new PipelineInputAssemblyStateCreateInfo
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = PrimitiveTopology.TriangleList,
                PrimitiveRestartEnable = false
            };
            var viewportState = new PipelineViewportStateCreateInfo
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                ScissorCount = 1
            };
            var dynamicState = new PipelineDynamicStateCreateInfo
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = (uint)dynamicStates.Length,
                PDynamicStates = dynamicStatesPointer
            };
            var rasterization = new PipelineRasterizationStateCreateInfo
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                DepthClampEnable = false,
                RasterizerDiscardEnable = false,
                PolygonMode = PolygonMode.Fill,
                CullMode = CullModeFlags.None,
                FrontFace = FrontFace.CounterClockwise,
                DepthBiasEnable = false,
                LineWidth = 1.0f
            };
            var multisampling = new PipelineMultisampleStateCreateInfo
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                RasterizationSamples = SampleCountFlags.Count1Bit,
                SampleShadingEnable = false
            };
            var depthStencil = new PipelineDepthStencilStateCreateInfo
            {
                SType = StructureType.PipelineDepthStencilStateCreateInfo,
                DepthTestEnable = true,
                DepthWriteEnable = true,
                DepthCompareOp = CompareOp.LessOrEqual,
                DepthBoundsTestEnable = false,
                StencilTestEnable = false
            };
            var colorBlend = new PipelineColorBlendStateCreateInfo
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = false,
                AttachmentCount = 0,
                PAttachments = null
            };
            var renderingInfo = new PipelineRenderingCreateInfo
            {
                SType = StructureType.PipelineRenderingCreateInfo,
                ColorAttachmentCount = 0,
                PColorAttachmentFormats = null,
                DepthAttachmentFormat = Format.D32Sfloat,
                StencilAttachmentFormat = Format.Undefined
            };
            var pipelineInfo = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                PNext = &renderingInfo,
                StageCount = 1,
                PStages = &vertexStage,
                PVertexInputState = &vertexInput,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PDynamicState = &dynamicState,
                PRasterizationState = &rasterization,
                PMultisampleState = &multisampling,
                PDepthStencilState = &depthStencil,
                PColorBlendState = &colorBlend,
                Layout = Ctx.ShadowDepthRigidPipelineLayout,
                RenderPass = default
            };
            if (Ctx.Vk.CreateGraphicsPipelines(Ctx.Device, default, 1, in pipelineInfo, null,
                    out Ctx.ShadowDepthRigidPipeline) != Result.Success)
            {
                throw new InvalidOperationException("Failed to create the shadow depth graphics pipeline.");
            }
        }

        Ctx.Vk.DestroyShaderModule(Ctx.Device, shaderModule, null);
        SilkMarshal.Free((nint)vertexStage.PName);
    }

    private Pipeline CreateComputePipeline(byte[] shaderCode, DescriptorSetLayout[] setLayouts,
        out PipelineLayout pipelineLayout)
    {
        var shaderModule = CreateShaderModule(shaderCode);
        fixed (DescriptorSetLayout* setLayoutsPointer = setLayouts)
        {
            var layoutInfo = new PipelineLayoutCreateInfo
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = (uint)setLayouts.Length,
                PSetLayouts = setLayoutsPointer
            };
            if (Ctx.Vk!.CreatePipelineLayout(Ctx.Device, &layoutInfo, null, out pipelineLayout) != Result.Success)
            {
                throw new InvalidOperationException("Failed to create the shadow compute pipeline layout.");
            }
        }

        var stageInfo = new PipelineShaderStageCreateInfo
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.ComputeBit,
            Module = shaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        };
        var pipelineInfo = new ComputePipelineCreateInfo
        {
            SType = StructureType.ComputePipelineCreateInfo,
            Stage = stageInfo,
            Layout = pipelineLayout
        };
        if (Ctx.Vk!.CreateComputePipelines(Ctx.Device, default, 1, &pipelineInfo, null, out var pipeline) !=
            Result.Success)
        {
            Ctx.Vk.DestroyPipelineLayout(Ctx.Device, pipelineLayout, null);
            pipelineLayout = default;
            throw new InvalidOperationException("Failed to create a shadow compute pipeline.");
        }

        Ctx.Vk.DestroyShaderModule(Ctx.Device, shaderModule, null);
        SilkMarshal.Free((nint)stageInfo.PName);
        return pipeline;
    }

    private static byte[] LoadEmbeddedShader(string fileName)
    {
        var assembly = typeof(VulkanShadowRenderer).Assembly;
        using var stream = assembly.GetManifestResourceStream($"Solas.Render.StandardShaders.Embedded.{fileName}")
                           ?? throw new FileNotFoundException(
                               $"Embedded shadow shader resource '{fileName}' was not found.");
        var shaderCode = new byte[stream.Length];
        stream.ReadExactly(shaderCode);
        return shaderCode;
    }

    private ShaderModule CreateShaderModule(byte[] shaderCode)
    {
        fixed (byte* shaderCodePointer = shaderCode)
        {
            var createInfo = new ShaderModuleCreateInfo
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)shaderCode.Length,
                PCode = (uint*)shaderCodePointer
            };
            if (Ctx.Vk!.CreateShaderModule(Ctx.Device, &createInfo, null, out var shaderModule) != Result.Success)
            {
                throw new InvalidOperationException("Failed to create a Vulkan shadow shader module.");
            }

            return shaderModule;
        }
    }

    private void RenderShadowRecord(CommandBuffer commandBuffer, IReadOnlyList<int> renderObjectIndices,
        uint shadowRecordIndex, ImageView depthView)
    {
        var clearValue = new ClearValue
        {
            DepthStencil = new ClearDepthStencilValue(1.0f, 0)
        };
        var depthAttachment = new RenderingAttachmentInfo
        {
            SType = StructureType.RenderingAttachmentInfo,
            ImageView = depthView,
            ImageLayout = ImageLayout.DepthStencilAttachmentOptimal,
            ResolveMode = ResolveModeFlags.None,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            ClearValue = clearValue
        };
        var renderingInfo = new RenderingInfo
        {
            SType = StructureType.RenderingInfo,
            RenderArea = new Rect2D(new Offset2D(0, 0),
                new Extent2D(Ctx.Settings.ShadowMapResolution, Ctx.Settings.ShadowMapResolution)),
            LayerCount = 1,
            ColorAttachmentCount = 0,
            PColorAttachments = null,
            PDepthAttachment = &depthAttachment,
            PStencilAttachment = null
        };

        Ctx.Vk!.CmdBeginRendering(commandBuffer, &renderingInfo);
        var viewport = new Viewport(
            0.0f,
            0.0f,
            Ctx.Settings.ShadowMapResolution,
            Ctx.Settings.ShadowMapResolution,
            0.0f,
            1.0f);
        Ctx.Vk.CmdSetViewport(commandBuffer, 0, 1, &viewport);
        var scissor = new Rect2D(new Offset2D(0, 0),
            new Extent2D(Ctx.Settings.ShadowMapResolution, Ctx.Settings.ShadowMapResolution));
        Ctx.Vk.CmdSetScissor(commandBuffer, 0, 1, &scissor);
        Ctx.Vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, Ctx.ShadowDepthRigidPipeline);

        for (var objectIndex = 0; objectIndex < renderObjectIndices.Count; objectIndex++)
        {
            var renderObject = Ctx.RenderData[renderObjectIndices[objectIndex]];
            if (renderObject.GpuMesh == null)
            {
                continue;
            }

            var vertexBuffer = renderObject.GpuMesh.VertexBuffer;
            ulong vertexOffset = 0;
            Ctx.Vk.CmdBindVertexBuffers(commandBuffer, 0, 1, in vertexBuffer, in vertexOffset);
            Ctx.Vk.CmdBindIndexBuffer(commandBuffer, renderObject.GpuMesh.IndexBuffer, 0, IndexType.Uint32);
            DescriptorSet[] descriptorSets =
            [
                Ctx.LightingGlobalSetsSet0[Ctx.FrameIndex],
                renderObject.DescriptorSets[Ctx.FrameIndex]
            ];
            fixed (DescriptorSet* descriptorSetsPointer = descriptorSets)
            {
                Ctx.Vk.CmdBindDescriptorSets(
                    commandBuffer,
                    PipelineBindPoint.Graphics,
                    Ctx.ShadowDepthRigidPipelineLayout,
                    0,
                    (uint)descriptorSets.Length,
                    descriptorSetsPointer,
                    0,
                    null);
            }

            Ctx.Vk.CmdPushConstants(
                commandBuffer,
                Ctx.ShadowDepthRigidPipelineLayout,
                ShaderStageFlags.VertexBit,
                0,
                sizeof(uint),
                &shadowRecordIndex);
            Ctx.Vk.CmdDrawIndexed(
                commandBuffer,
                renderObject.GpuMesh.IndexCount,
                1,
                0,
                0,
                0);
        }

        Ctx.Vk.CmdEndRendering(commandBuffer);
    }

    private void BarrierComputeWritesToShadowConsumers(CommandBuffer commandBuffer)
    {
        var barrier = new MemoryBarrier2
        {
            SType = StructureType.MemoryBarrier2,
            SrcStageMask = PipelineStageFlags2.ComputeShaderBit,
            SrcAccessMask = AccessFlags2.ShaderWriteBit,
            DstStageMask = PipelineStageFlags2.ComputeShaderBit | PipelineStageFlags2.VertexShaderBit |
                           PipelineStageFlags2.FragmentShaderBit,
            DstAccessMask = AccessFlags2.ShaderReadBit
        };
        var dependencyInfo = new DependencyInfo
        {
            SType = StructureType.DependencyInfo,
            MemoryBarrierCount = 1,
            PMemoryBarriers = &barrier
        };
        Ctx.Vk!.CmdPipelineBarrier2(commandBuffer, &dependencyInfo);
    }

    private void TransitionShadowImages(CommandBuffer commandBuffer, ImageLayout oldLayout, ImageLayout newLayout,
        AccessFlags2 sourceAccess, AccessFlags2 destinationAccess, PipelineStageFlags2 sourceStage,
        PipelineStageFlags2 destinationStage)
    {
        ImageMemoryBarrier2[] barriers =
        [
            CreateShadowImageBarrier(
                Ctx.ShadowDepthArrayImage,
                (uint)Ctx.ShadowDepthLayerViews.Length,
                oldLayout,
                newLayout,
                sourceAccess,
                destinationAccess,
                sourceStage,
                destinationStage),
            CreateShadowImageBarrier(
                Ctx.PointShadowCubeArrayImage,
                (uint)Ctx.PointShadowFaceViews.Length,
                oldLayout,
                newLayout,
                sourceAccess,
                destinationAccess,
                sourceStage,
                destinationStage)
        ];
        fixed (ImageMemoryBarrier2* barriersPointer = barriers)
        {
            var dependencyInfo = new DependencyInfo
            {
                SType = StructureType.DependencyInfo,
                ImageMemoryBarrierCount = (uint)barriers.Length,
                PImageMemoryBarriers = barriersPointer
            };
            Ctx.Vk!.CmdPipelineBarrier2(commandBuffer, &dependencyInfo);
        }
    }

    private static ImageMemoryBarrier2 CreateShadowImageBarrier(Image image, uint layerCount, ImageLayout oldLayout,
        ImageLayout newLayout, AccessFlags2 sourceAccess, AccessFlags2 destinationAccess,
        PipelineStageFlags2 sourceStage, PipelineStageFlags2 destinationStage)
    {
        return new ImageMemoryBarrier2
        {
            SType = StructureType.ImageMemoryBarrier2,
            SrcStageMask = sourceStage,
            SrcAccessMask = sourceAccess,
            DstStageMask = destinationStage,
            DstAccessMask = destinationAccess,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.DepthBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = layerCount
            }
        };
    }

    private void DestroyPipeline(ref Pipeline pipeline)
    {
        if (pipeline.Handle != 0)
        {
            Ctx.Vk!.DestroyPipeline(Ctx.Device, pipeline, null);
            pipeline = default;
        }
    }

    private void DestroyPipelineLayout(ref PipelineLayout pipelineLayout)
    {
        if (pipelineLayout.Handle != 0)
        {
            Ctx.Vk!.DestroyPipelineLayout(Ctx.Device, pipelineLayout, null);
            pipelineLayout = default;
        }
    }
}