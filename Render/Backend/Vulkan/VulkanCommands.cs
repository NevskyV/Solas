using System;
using System.Numerics;
using Silk.NET.Vulkan;
using Solas.Render.Components;
using Solas.Render.Vulkan.Components;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanCommands : VulkanInjectable
{
    #region Command Pool

    internal void CreateCommandPool()
    {
        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit
        };

        if (Ctx.Vk!.CreateCommandPool(Ctx.Device, &poolInfo, null, out Ctx.CommandPool) != Result.Success)
        {
            throw new Exception("failed to create command pool!");
        }
    }

    #endregion

    #region CommandBuffer

    internal void CreateCommandBuffers()
    {
        Ctx.CommandBuffers = new CommandBuffer[Ctx.Settings.MaxFramesInFlight];

        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = Ctx.CommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = Ctx.Settings.MaxFramesInFlight
        };
        fixed (CommandBuffer* commandBuffersPtr = Ctx.CommandBuffers)
        {
            if (Ctx.Vk!.AllocateCommandBuffers(Ctx.Device, in allocInfo, commandBuffersPtr) != Result.Success)
            {
                throw new Exception("failed to allocate command buffers!");
            }
        }
    }

    internal void RecordCommandBuffer(uint imageIndex)
    {
        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.None
        };

        if (Ctx.Vk!.BeginCommandBuffer(Ctx.CommandBuffers![Ctx.FrameIndex], &beginInfo) != Result.Success)
        {
            throw new Exception("Failed to begin command buffer");
        }

        bool isScaling = Ctx.RenderExtent.Width != Ctx.SwapChainExtent.Width ||
                         Ctx.RenderExtent.Height != Ctx.SwapChainExtent.Height;
        bool isMsaa = Ctx.MsaaSamples != SampleCountFlags.Count1Bit;

        Image srcBlitImage = default;

        TransitionImageLayout(
            Ctx.ColorImage,
            ImageLayout.Undefined,
            ImageLayout.ColorAttachmentOptimal,
            AccessFlags2.None,
            AccessFlags2.ColorAttachmentWriteBit,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            ImageAspectFlags.ColorBit
        );

        if (isScaling && isMsaa)
        {
            TransitionImageLayout(
                Ctx.ResolveImage,
                ImageLayout.Undefined,
                ImageLayout.ColorAttachmentOptimal,
                AccessFlags2.None,
                AccessFlags2.ColorAttachmentWriteBit,
                PipelineStageFlags2.ColorAttachmentOutputBit,
                PipelineStageFlags2.ColorAttachmentOutputBit,
                ImageAspectFlags.ColorBit
            );
        }

        TransitionImageLayout(
            Ctx.SwapChainImages![imageIndex],
            ImageLayout.Undefined,
            isScaling ? ImageLayout.TransferDstOptimal : ImageLayout.ColorAttachmentOptimal,
            AccessFlags2.None,
            isScaling ? AccessFlags2.TransferWriteBit : AccessFlags2.ColorAttachmentWriteBit,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            isScaling ? PipelineStageFlags2.TransferBit : PipelineStageFlags2.ColorAttachmentOutputBit,
            ImageAspectFlags.ColorBit
        );

        TransitionImageLayout(
            Ctx.DepthImage,
            ImageLayout.Undefined,
            ImageLayout.DepthAttachmentOptimal,
            AccessFlags2.DepthStencilAttachmentWriteBit,
            AccessFlags2.DepthStencilAttachmentWriteBit,
            PipelineStageFlags2.EarlyFragmentTestsBit | PipelineStageFlags2.LateFragmentTestsBit,
            PipelineStageFlags2.EarlyFragmentTestsBit | PipelineStageFlags2.LateFragmentTestsBit,
            ImageAspectFlags.DepthBit
        );

        ClearValue clearColor = new ClearValue() { Color = new ClearColorValue(0.1f, 0.1f, 0.15f, 1.0f) };
        ClearValue clearDepth = new ClearValue() { DepthStencil = new ClearDepthStencilValue(1.0f, 0) };

        ImageView resolveTargetView = default;
        if (isMsaa)
        {
            if (isScaling)
            {
                resolveTargetView = Ctx.ResolveImageView;
                srcBlitImage = Ctx.ResolveImage;
            }
            else
            {
                resolveTargetView = Ctx.SwapChainImageViews![imageIndex];
            }
        }
        else
        {
            srcBlitImage = Ctx.ColorImage;
        }

        var attachmentInfo = new RenderingAttachmentInfo
        {
            SType = StructureType.RenderingAttachmentInfo,
            ImageView = Ctx.ColorImageView,
            ImageLayout = ImageLayout.ColorAttachmentOptimal,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = (isScaling && isMsaa) ? AttachmentStoreOp.Store : AttachmentStoreOp.DontCare,
            ClearValue = clearColor,
            ResolveMode = isMsaa ? ResolveModeFlags.AverageBit : ResolveModeFlags.None,
            ResolveImageView = isMsaa ? resolveTargetView : default,
            ResolveImageLayout = isMsaa ? ImageLayout.ColorAttachmentOptimal : ImageLayout.Undefined
        };

        var depthAttachmentInfo = new RenderingAttachmentInfo
        {
            SType = StructureType.RenderingAttachmentInfo,
            ImageView = Ctx.DepthImageView,
            ImageLayout = ImageLayout.DepthAttachmentOptimal,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.DontCare,
            ClearValue = clearDepth,
        };

        var renderingInfo = new RenderingInfo
        {
            SType = StructureType.RenderingInfo,
            RenderArea = new Rect2D(new Offset2D(0, 0), Ctx.RenderExtent),
            LayerCount = 1,
            ColorAttachmentCount = 1,
            PColorAttachments = &attachmentInfo,
            PDepthAttachment = &depthAttachmentInfo
        };

        var cmdBuffer = Ctx.CommandBuffers![Ctx.FrameIndex];
        uint frameIdx = Ctx.FrameIndex;

        uint zeroCounter = 0;
        System.Buffer.MemoryCopy(&zeroCounter, Ctx.GlobalIndexCounterMappedPointers[frameIdx], 4, 4);

        uint tileCountX = (uint)MathF.Ceiling(Ctx.RenderExtent.Width / (float)Ctx.Settings.TileSize);
        uint tileCountY = (uint)MathF.Ceiling(Ctx.RenderExtent.Height / (float)Ctx.Settings.TileSize);

        Matrix4x4.Invert(Ctx.CameraProjectionMatrix, out Matrix4x4 invProj);

        var activeLights = LightDataEventHandler.GpuLights;

        FrameParamsGpu frameParams = new()
        {
            ViewMatrix = Matrix4x4.Transpose(Ctx.CameraViewMatrix),
            InvProjectionMatrix = Matrix4x4.Transpose(invProj),
            ScreenResolution = new Vector2(Ctx.RenderExtent.Width, Ctx.RenderExtent.Height),
            TileCount = new Vector2(tileCountX, tileCountY),
            TotalLightCount = (uint)activeLights.Length
        };

        System.Buffer.MemoryCopy(&frameParams, Ctx.FrameParamsMappedPointers[frameIdx], sizeof(FrameParamsGpu), sizeof(FrameParamsGpu));

        fixed (LightGpu* pLights = activeLights)
        {
            uint lightsSize = (uint)(sizeof(LightGpu) * activeLights.Length);
            System.Buffer.MemoryCopy(pLights, Ctx.LightBuffersMappedPointers[frameIdx], lightsSize, lightsSize);
        }

        Ctx.Vk!.CmdBindPipeline(cmdBuffer, PipelineBindPoint.Compute, Ctx.LightCullingPipeline);

        DescriptorSet[] computeSets = [Ctx.LightingGlobalSetsSet0[frameIdx], Ctx.LightingFrameSetsSet1[frameIdx]];
        fixed (DescriptorSet* pComputeSets = computeSets)
        {
            Ctx.Vk!.CmdBindDescriptorSets(cmdBuffer, PipelineBindPoint.Compute, Ctx.LightCullingPipelineLayout, 0, (uint)computeSets.Length, pComputeSets, 0, null);
        }

        Ctx.Vk!.CmdDispatch(cmdBuffer, tileCountX, tileCountY, 1);

        MemoryBarrier2 memoryBarrier = new()
        {
            SType = StructureType.MemoryBarrier2,
            SrcStageMask = PipelineStageFlags2.ComputeShaderBit,
            SrcAccessMask = AccessFlags2.ShaderWriteBit,
            DstStageMask = PipelineStageFlags2.FragmentShaderBit,
            DstAccessMask = AccessFlags2.ShaderReadBit
        };

        DependencyInfo dependencyInfo = new()
        {
            SType = StructureType.DependencyInfo,
            MemoryBarrierCount = 1,
            PMemoryBarriers = &memoryBarrier
        };

        Ctx.Vk!.CmdPipelineBarrier2(cmdBuffer, &dependencyInfo);

        Ctx.Vk.CmdBeginRendering(cmdBuffer, &renderingInfo);

        var viewport = new Viewport(0.0f, 0.0f, Ctx.RenderExtent.Width, Ctx.RenderExtent.Height, 0.0f, 1.0f);
        Ctx.Vk.CmdSetViewport(cmdBuffer, 0, 1, &viewport);

        var scissor = new Rect2D(new Offset2D(0, 0), Ctx.RenderExtent);
        Ctx.Vk.CmdSetScissor(cmdBuffer, 0, 1, &scissor);

        Buffer lastVertexBuffer = default;
        Pipeline lastPipeline = default;

        foreach (var renderObject in Ctx.RenderData)
        {
            if (renderObject.GpuMesh == null || renderObject.GpuTexture == null) continue;

            var pipeline = renderObject.MaterialPipeline.Pipeline;
            var layout = renderObject.MaterialPipeline.Layout;

            if (pipeline.Handle != lastPipeline.Handle)
            {
                Ctx.Vk.CmdBindPipeline(cmdBuffer, PipelineBindPoint.Graphics, pipeline);
                lastPipeline = pipeline;
            }

            var gpuMesh = renderObject.GpuMesh;

            if (gpuMesh.VertexBuffer.Handle != lastVertexBuffer.Handle)
            {
                ulong offset = 0;
                Ctx.Vk.CmdBindVertexBuffers(cmdBuffer, 0, 1, in gpuMesh.VertexBuffer, in offset);
                Ctx.Vk.CmdBindIndexBuffer(cmdBuffer, gpuMesh.IndexBuffer, 0, IndexType.Uint32);
                lastVertexBuffer = gpuMesh.VertexBuffer;
            }

            DescriptorSet lightingSet0 = Ctx.LightingGlobalSetsSet0[Ctx.FrameIndex];
            DescriptorSet objectMaterialSet1 = renderObject.DescriptorSets[Ctx.FrameIndex];

            DescriptorSet[] descriptorSets = [lightingSet0, objectMaterialSet1];

            fixed (DescriptorSet* pDescriptorSets = descriptorSets)
            {
                Ctx.Vk.CmdBindDescriptorSets(
                    cmdBuffer,
                    PipelineBindPoint.Graphics,
                    layout,
                    0,
                    (uint)descriptorSets.Length,
                    pDescriptorSets,
                    0,
                    null
                );
            }

            Ctx.Vk.CmdDrawIndexed(cmdBuffer, gpuMesh.IndexCount, 1, 0, 0, 0);
        }

        Ctx.Vk.CmdEndRendering(cmdBuffer);

        if (isScaling)
        {
            TransitionImageLayout(
                srcBlitImage,
                ImageLayout.ColorAttachmentOptimal,
                ImageLayout.TransferSrcOptimal,
                AccessFlags2.ColorAttachmentWriteBit,
                AccessFlags2.TransferReadBit,
                PipelineStageFlags2.ColorAttachmentOutputBit,
                PipelineStageFlags2.TransferBit,
                ImageAspectFlags.ColorBit
            );

            var blitRegion = new ImageBlit2
            {
                SType = StructureType.ImageBlit2,
                SrcSubresource = new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = 0,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                },
                DstSubresource = new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = 0,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };
            blitRegion.SrcOffsets[0] = new Offset3D(0, 0, 0);
            blitRegion.SrcOffsets[1] = new Offset3D((int)Ctx.RenderExtent.Width, (int)Ctx.RenderExtent.Height, 1);
            blitRegion.DstOffsets[0] = new Offset3D(0, 0, 0);
            blitRegion.DstOffsets[1] = new Offset3D((int)Ctx.SwapChainExtent.Width, (int)Ctx.SwapChainExtent.Height, 1);

            var blitInfo = new BlitImageInfo2
            {
                SType = StructureType.BlitImageInfo2,
                SrcImage = srcBlitImage,
                SrcImageLayout = ImageLayout.TransferSrcOptimal,
                DstImage = Ctx.SwapChainImages![imageIndex],
                DstImageLayout = ImageLayout.TransferDstOptimal,
                RegionCount = 1,
                PRegions = &blitRegion,
                Filter = Filter.Linear
            };

            Ctx.Vk.CmdBlitImage2(cmdBuffer, &blitInfo);

            TransitionImageLayout(
                Ctx.SwapChainImages![imageIndex],
                ImageLayout.TransferDstOptimal,
                ImageLayout.PresentSrcKhr,
                AccessFlags2.TransferWriteBit,
                AccessFlags2.None,
                PipelineStageFlags2.TransferBit,
                PipelineStageFlags2.BottomOfPipeBit,
                ImageAspectFlags.ColorBit
            );
        }
        else
        {
            TransitionImageLayout(
                Ctx.SwapChainImages![imageIndex],
                ImageLayout.ColorAttachmentOptimal,
                ImageLayout.PresentSrcKhr,
                AccessFlags2.ColorAttachmentWriteBit,
                AccessFlags2.None,
                PipelineStageFlags2.ColorAttachmentOutputBit,
                PipelineStageFlags2.BottomOfPipeBit,
                ImageAspectFlags.ColorBit
            );
        }

        if (Ctx.Vk!.EndCommandBuffer(cmdBuffer) != Result.Success)
        {
            throw new Exception("Failed to end command buffer");
        }
    }

    private void TransitionImageLayout(
        Image image,
        ImageLayout oldLayout,
        ImageLayout newLayout,
        AccessFlags2 srcAccessMask,
        AccessFlags2 dstAccessMask,
        PipelineStageFlags2 srcStageMask,
        PipelineStageFlags2 dstStageMask,
        ImageAspectFlags imageAspectFlags)
    {
        var barrier = new ImageMemoryBarrier2
        {
            SType = StructureType.ImageMemoryBarrier2,
            SrcStageMask = srcStageMask,
            SrcAccessMask = srcAccessMask,
            DstStageMask = dstStageMask,
            DstAccessMask = dstAccessMask,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = imageAspectFlags,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        var dependencyInfo = new DependencyInfo
        {
            SType = StructureType.DependencyInfo,
            ImageMemoryBarrierCount = 1,
            PImageMemoryBarriers = &barrier
        };

        Ctx.Vk!.CmdPipelineBarrier2(Ctx.CommandBuffers![Ctx.FrameIndex], &dependencyInfo);
    }

    #endregion
}