using Silk.NET.Vulkan;
using Solas.Render.Vulkan.Extensions;
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

        // 2. Transition swapchain image layout to ColorAttachmentOptimal
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
            ImageView = Ctx.SwapChainImageViews![imageIndex],
            ImageLayout = ImageLayout.ColorAttachmentOptimal,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            ClearValue = clearColor
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

        // 4. Record drawing commands
        Ctx.Vk.CmdBeginRendering(Ctx.CommandBuffers![Ctx.FrameIndex], &renderingInfo);

        Ctx.Vk.CmdBindPipeline(Ctx.CommandBuffers![Ctx.FrameIndex], PipelineBindPoint.Graphics, Ctx.GraphicsPipeline);

        fixed (Buffer* pBuffer = &Ctx.VertexBuffer)
        {
            Ctx.Vk.CmdBindVertexBuffers(Ctx.CommandBuffers![Ctx.FrameIndex], 0, pBuffer, new Span<ulong>([0]));
        }


        Ctx.Vk.CmdBindIndexBuffer(Ctx.CommandBuffers![Ctx.FrameIndex], Ctx.IndexBuffer, 0, IndexType.Uint32);

        // Viewport setup
        var viewport = new Viewport(0.0f, 0.0f, Ctx.SwapChainExtent.Width, Ctx.SwapChainExtent.Height, 0.0f, 1.0f);
        Ctx.Vk.CmdSetViewport(Ctx.CommandBuffers![Ctx.FrameIndex], 0, 1, &viewport);

        // Scissor setup
        var scissor = new Rect2D(new Offset2D(0, 0), Ctx.SwapChainExtent);
        Ctx.Vk.CmdSetScissor(Ctx.CommandBuffers![Ctx.FrameIndex], 0, 1, &scissor);


        Ctx.Vk!.CmdBindDescriptorSets(Ctx.CommandBuffers![Ctx.FrameIndex], PipelineBindPoint.Graphics,
            Ctx.PipelineLayout, 0, 1, in Ctx.DescriptorSets![Ctx.FrameIndex], 0, null);

        Ctx.Vk.CmdDrawIndexed(Ctx.CommandBuffers![Ctx.FrameIndex], (uint)Ctx.Indices.Length, 1, 0, 0, 0);

        Ctx.Vk.CmdEndRendering(Ctx.CommandBuffers![Ctx.FrameIndex]);

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
        if (Ctx.Vk!.EndCommandBuffer(Ctx.CommandBuffers![Ctx.FrameIndex]) != Result.Success)
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