using System.Numerics;
using Silk.NET.Vulkan;
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

        if (Ctx.Vk!.CreateCommandPool(Ctx.Device, &poolInfo, null, out Ctx.CommandPool) !=
            Result.Success)
        {
            throw new Exception("failed to create command pool!");
        }
    }

    #endregion

    #region CommandBuffer

    internal void CreateCommandBuffers()
    {
        Ctx.CommandBuffers = new CommandBuffer[Ctx.MaxFramesInFlight];

        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = Ctx.CommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = Ctx.MaxFramesInFlight
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

        // 1. Transition from MSAA Color Image into ColorAttachmentOptimal
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

        // 2. Transition from Swapchain Image to ColorAttachmentOptimal
        TransitionImageLayout(
            Ctx.SwapChainImages![imageIndex],
            ImageLayout.Undefined,
            ImageLayout.ColorAttachmentOptimal,
            AccessFlags2.None,
            AccessFlags2.ColorAttachmentWriteBit,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            ImageAspectFlags.ColorBit
        );

        // 3. Transition from MSAA Depth Image into DepthAttachmentOptimal
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

        // 3. Define clear color and dynamic rendering attachments
        ClearValue clearColor = new ClearValue() { Color = new ClearColorValue(0.0f, 0.0f, 0.0f, 1.0f) };
        ClearValue clearDepth = new ClearValue() { DepthStencil = new ClearDepthStencilValue(1.0f, 0) };

        var attachmentInfo = new RenderingAttachmentInfo
        {
            SType = StructureType.RenderingAttachmentInfo,
            ImageView = Ctx.ColorImageView,
            ImageLayout = ImageLayout.ColorAttachmentOptimal,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.DontCare,
            ClearValue = clearColor,
            ResolveMode = ResolveModeFlags.AverageBit,
            ResolveImageView = Ctx.SwapChainImageViews![imageIndex],
            ResolveImageLayout = ImageLayout.ColorAttachmentOptimal
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
            RenderArea = new Rect2D(new Offset2D(0, 0), Ctx.SwapChainExtent),
            LayerCount = 1,
            ColorAttachmentCount = 1,
            PColorAttachments = &attachmentInfo,
            PDepthAttachment = &depthAttachmentInfo
        };

        var cmdBuffer = Ctx.CommandBuffers![Ctx.FrameIndex];

        uint frameIdx = Ctx.FrameIndex;

        uint zeroCounter = 0;
        System.Buffer.MemoryCopy(&zeroCounter, Ctx.GlobalIndexCounterMappedPointers[frameIdx], 4, 4);

        uint tileCountX = (uint)MathF.Ceiling(Ctx.SwapChainExtent.Width / 16.0f);
        uint tileCountY = (uint)MathF.Ceiling(Ctx.SwapChainExtent.Height / 16.0f);

        Matrix4x4.Invert(Ctx.CameraProjectionMatrix, out Matrix4x4 invProj);

        FrameParamsGpu frameParams = new()
        {
            ViewMatrix = Matrix4x4.Transpose(Ctx.CameraViewMatrix),
            InvProjectionMatrix = Matrix4x4.Transpose(invProj),
            ScreenResolution = new Vector2(Ctx.SwapChainExtent.Width, Ctx.SwapChainExtent.Height),
            TileCount = new Vector2(tileCountX, tileCountY),
            TotalLightCount = (uint)Ctx.ActiveLights.Length
        };

        System.Buffer.MemoryCopy(&frameParams, Ctx.FrameParamsMappedPointers[frameIdx], sizeof(FrameParamsGpu),
            sizeof(FrameParamsGpu));


        fixed (PointLightGpu* pLights = Ctx.ActiveLights)
        {
            uint lightsSize = (uint)(sizeof(PointLightGpu) * Ctx.ActiveLights.Length);
            System.Buffer.MemoryCopy(pLights, Ctx.LightBuffersMappedPointers[frameIdx], lightsSize, lightsSize);
        }

        Ctx.Vk!.CmdBindPipeline(cmdBuffer, PipelineBindPoint.Compute, Ctx.LightCullingPipeline);

        DescriptorSet[] computeSets = [Ctx.LightingGlobalSetsSet0[frameIdx], Ctx.LightingFrameSetsSet1[frameIdx]];
        fixed (DescriptorSet* pComputeSets = computeSets)
        {
            Ctx.Vk!.CmdBindDescriptorSets(cmdBuffer, PipelineBindPoint.Compute, Ctx.LightCullingPipelineLayout, 0,
                (uint)computeSets.Length, pComputeSets, 0, null);
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

        // 4. Record drawing commands
        Ctx.Vk.CmdBeginRendering(cmdBuffer, &renderingInfo);

        Ctx.Vk.CmdBindPipeline(cmdBuffer, PipelineBindPoint.Graphics, Ctx.GraphicsPipeline);

        // Viewport setup
        var viewport = new Viewport(0.0f, 0.0f, Ctx.SwapChainExtent.Width, Ctx.SwapChainExtent.Height, 0.0f, 1.0f);
        Ctx.Vk.CmdSetViewport(cmdBuffer, 0, 1, &viewport);

        // Scissor setup
        var scissor = new Rect2D(new Offset2D(0, 0), Ctx.SwapChainExtent);
        Ctx.Vk.CmdSetScissor(cmdBuffer, 0, 1, &scissor);

        Buffer lastVertexBuffer = default;

        foreach (var renderObject in Ctx.RenderData)
        {
            if (renderObject.GpuMesh == null || renderObject.GpuTexture == null) continue;

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
                    Ctx.PipelineLayout,
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

        // 5. Transition layout back to PresentSrcKhr
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

        // 6. End command buffer recording
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