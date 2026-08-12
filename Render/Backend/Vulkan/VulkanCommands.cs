using System;
using System.Numerics;
using Silk.NET.Vulkan;
using Solas.Render.Components;
using Solas.Render.Vulkan.Components;
using Solas.Transform.MathExtensions;
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

        bool hasScreenMaterial = Ctx.CameraData.ScreenMaterial != null;
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

        if (isMsaa && (isScaling || hasScreenMaterial))
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
            (isScaling && !hasScreenMaterial) ? ImageLayout.TransferDstOptimal : ImageLayout.ColorAttachmentOptimal,
            AccessFlags2.None,
            (isScaling && !hasScreenMaterial) ? AccessFlags2.TransferWriteBit : AccessFlags2.ColorAttachmentWriteBit,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            (isScaling && !hasScreenMaterial)
                ? PipelineStageFlags2.TransferBit
                : PipelineStageFlags2.ColorAttachmentOutputBit,
            ImageAspectFlags.ColorBit
        );

        TransitionImageLayout(
            Ctx.DepthImage,
            ImageLayout.Undefined,
            ImageLayout.DepthStencilAttachmentOptimal,
            AccessFlags2.DepthStencilAttachmentWriteBit,
            AccessFlags2.DepthStencilAttachmentWriteBit,
            PipelineStageFlags2.EarlyFragmentTestsBit | PipelineStageFlags2.LateFragmentTestsBit,
            PipelineStageFlags2.EarlyFragmentTestsBit | PipelineStageFlags2.LateFragmentTestsBit,
            ImageAspectFlags.DepthBit | ImageAspectFlags.StencilBit
        );

        var camColor = Ctx.CameraData.BackgroundColor;
        ClearValue clearColor = new ClearValue()
            { Color = new ClearColorValue(camColor.X, camColor.Y, camColor.Z, 1.0f) };
        ClearValue clearDepth = new ClearValue() { DepthStencil = new ClearDepthStencilValue(1.0f, 0) };

        ImageView resolveTargetView = default;
        if (isMsaa)
        {
            if (isScaling || hasScreenMaterial)
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
            ImageLayout = ImageLayout.DepthStencilAttachmentOptimal,
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
            PDepthAttachment = &depthAttachmentInfo,
            PStencilAttachment = &depthAttachmentInfo
        };

        var cmdBuffer = Ctx.CommandBuffers![Ctx.FrameIndex];
        uint frameIdx = Ctx.FrameIndex;

        uint zeroCounter = 0;
        System.Buffer.MemoryCopy(&zeroCounter, Ctx.GlobalIndexCounterMappedPointers[frameIdx], 4, 4);

        float tileSizeX = Ctx.Settings.TileSize.Z > 1 ? Ctx.Settings.TileSize.X * 4f : Ctx.Settings.TileSize.X;
        float tileSizeY = Ctx.Settings.TileSize.Z > 1 ? Ctx.Settings.TileSize.Y * 4f : Ctx.Settings.TileSize.Y;

        uint tileCountX = (uint)MathF.Ceiling(Ctx.RenderExtent.Width / tileSizeX);
        uint tileCountY = (uint)MathF.Ceiling(Ctx.RenderExtent.Height / tileSizeY);
        uint tileCountZ = (uint)Math.Max(1, (int)Ctx.Settings.TileSize.Z);

        bool isOrtho = Ctx.CameraData.Type == CameraType.Orthographic;

        float aspectRatioCompute = (float)Ctx.RenderExtent.Width / Ctx.RenderExtent.Height;
        float tanHalfFovY = MathF.Tan(Ctx.CameraData.FieldOfView * MathF.PI / 180f * 0.5f);
        float tanHalfFovX = tanHalfFovY * aspectRatioCompute;
        float orthoHalfW = Ctx.CameraData.Size * aspectRatioCompute * 0.5f;
        float orthoHalfH = Ctx.CameraData.Size * 0.5f;

        var activeLights = LightDataEventHandler.GetGpuLights(out uint directionalCount);

        Vector3 camPos = Ctx.CameraTransform.Position.Value;
        Vector3 camRot = Ctx.CameraTransform.Rotation.Value;
        Quaternion camQuat = camRot.ToQuaternion();
        Vector3 camForward = Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, camQuat));
        Vector3 camUp = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, camQuat));
        Vector3 camRight = Vector3.Normalize(Vector3.Cross(camForward, camUp));

        FrameParamsGpu frameParams = new()
        {
            CameraPosition = new Vector4(camPos, 1.0f),
            CameraRight = new Vector4(camRight, 0.0f),
            CameraUp = new Vector4(camUp, 0.0f),
            CameraForward = new Vector4(camForward, 0.0f),
            ScreenResolution = new Vector4(Ctx.RenderExtent.Width, Ctx.RenderExtent.Height, 0, 0),
            TileCount = new Vector4(tileCountX, tileCountY, tileCountZ, 0),
            TotalLightCount = (uint)activeLights.Length,
            DirectionalLightCount = directionalCount,
            NearClip = Ctx.CameraData.NearClipPlane,
            FarClip = Ctx.CameraData.FarClipPlane,
            IsOrthographic = isOrtho ? 1u : 0u,
            TanHalfFovX = isOrtho ? orthoHalfW : tanHalfFovX,
            TanHalfFovY = isOrtho ? orthoHalfH : tanHalfFovY
        };


        System.Buffer.MemoryCopy(&frameParams, Ctx.FrameParamsMappedPointers[frameIdx], sizeof(FrameParamsGpu),
            sizeof(FrameParamsGpu));


        fixed (LightGpu* pLights = activeLights)
        {
            uint lightsSize = (uint)(sizeof(LightGpu) * activeLights.Length);
            System.Buffer.MemoryCopy(pLights, Ctx.LightBuffersMappedPointers[frameIdx], lightsSize, lightsSize);
        }

        Ctx.Vk!.CmdFillBuffer(cmdBuffer, Ctx.GlobalIndexCounterBuffers[frameIdx], 0, sizeof(uint), 0);

        var bufferBarrier = new BufferMemoryBarrier2
        {
            SType = StructureType.BufferMemoryBarrier2,
            SrcStageMask = PipelineStageFlags2.TransferBit,
            SrcAccessMask = AccessFlags2.TransferWriteBit,
            DstStageMask = PipelineStageFlags2.ComputeShaderBit,
            DstAccessMask = AccessFlags2.ShaderReadBit | AccessFlags2.ShaderWriteBit,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Buffer = Ctx.GlobalIndexCounterBuffers[frameIdx],
            Offset = 0,
            Size = sizeof(uint)
        };

        DependencyInfo dependencyInfo = new()
        {
            SType = StructureType.DependencyInfo,
            BufferMemoryBarrierCount = 1,
            PBufferMemoryBarriers = &bufferBarrier
        };

        Ctx.Vk!.CmdPipelineBarrier2(cmdBuffer, &dependencyInfo);

        Ctx.Vk!.CmdBindPipeline(cmdBuffer, PipelineBindPoint.Compute, Ctx.LightCullingPipeline);
        DescriptorSet[] computeSets = [Ctx.LightingGlobalSetsSet0[frameIdx], Ctx.LightingFrameSetsSet1[frameIdx]];
        fixed (DescriptorSet* pComputeSets = computeSets)
        {
            Ctx.Vk!.CmdBindDescriptorSets(cmdBuffer, PipelineBindPoint.Compute, Ctx.LightCullingPipelineLayout, 0,
                (uint)computeSets.Length, pComputeSets, 0, null);
        }

        Ctx.Vk!.CmdDispatch(cmdBuffer, tileCountX, tileCountY, tileCountZ);

        var computeToFragBarrier = new MemoryBarrier2
        {
            SType = StructureType.MemoryBarrier2,
            SrcStageMask = PipelineStageFlags2.ComputeShaderBit,
            SrcAccessMask = AccessFlags2.ShaderWriteBit,
            DstStageMask = PipelineStageFlags2.FragmentShaderBit,
            DstAccessMask = AccessFlags2.ShaderReadBit
        };

        var computeToFragDependency = new DependencyInfo
        {
            SType = StructureType.DependencyInfo,
            MemoryBarrierCount = 1,
            PMemoryBarriers = &computeToFragBarrier
        };

        Ctx.Vk!.CmdPipelineBarrier2(cmdBuffer, &computeToFragDependency);

        Ctx.Vk.CmdBeginRendering(cmdBuffer, &renderingInfo);

        var viewport = new Viewport(0.0f, 0.0f, Ctx.RenderExtent.Width, Ctx.RenderExtent.Height, 0.0f, 1.0f);
        Ctx.Vk.CmdSetViewport(cmdBuffer, 0, 1, &viewport);

        var scissor = new Rect2D(new Offset2D(0, 0), Ctx.RenderExtent);
        Ctx.Vk.CmdSetScissor(cmdBuffer, 0, 1, &scissor);

        Buffer lastVertexBuffer = default;
        Pipeline lastPipeline = default;

        foreach (var renderObject in Ctx.RenderData)
        {
            if (renderObject.GpuMesh == null) continue;

            var material = renderObject.Material;
            var passes = material?.Passes;
            int passCount = passes != null && passes.Count > 0 ? passes.Count : 1;

            for (int passIdx = 0; passIdx < passCount; passIdx++)
            {
                VulkanMaterialPipeline materialPipeline;
                if (material != null && passes != null && passes.Count > 0)
                {
                    materialPipeline = Ctx.PipelineFactory.GetOrCreatePipeline(material, passes[passIdx], passIdx);
                }
                else
                {
                    materialPipeline = renderObject.MaterialPipeline;
                }

                var pipeline = materialPipeline.Pipeline;
                var layout = materialPipeline.Layout;

                if (pipeline.Handle != lastPipeline.Handle)
                {
                    Ctx.Vk.CmdBindPipeline(cmdBuffer, PipelineBindPoint.Graphics, pipeline);
                    lastPipeline = pipeline;
                }

                uint[] pushData = passes is { Count: > 0 }
                    ? [(uint)passes[passIdx].CullMode, passes[passIdx].DepthWrite ? 1u : 0u]
                    : [(uint)CullMode.Back, 1u];
                fixed (uint* pPushData = pushData)
                {
                    Ctx.Vk.CmdPushConstants(
                        cmdBuffer,
                        layout,
                        ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
                        0,
                        sizeof(uint) * 2,
                        pPushData
                    );
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
        }

        Ctx.Vk.CmdEndRendering(cmdBuffer);

        if (hasScreenMaterial)
        {
            Image srcImage = isMsaa ? Ctx.ResolveImage : Ctx.ColorImage;
            TransitionImageLayout(
                srcImage,
                ImageLayout.ColorAttachmentOptimal,
                ImageLayout.ShaderReadOnlyOptimal,
                AccessFlags2.ColorAttachmentWriteBit,
                AccessFlags2.ShaderReadBit,
                PipelineStageFlags2.ColorAttachmentOutputBit,
                PipelineStageFlags2.FragmentShaderBit,
                ImageAspectFlags.ColorBit
            );

            int passCount = Ctx.CameraData.ScreenMaterial!.PassCount;

            for (int p = 0; p < passCount; p++)
            {
                bool isFinalPass = p == passCount - 1;
                ImageView targetView;
                Extent2D targetExtent;
                Image? targetImage = null;

                if (isFinalPass)
                {
                    targetView = Ctx.SwapChainImageViews![imageIndex];
                    targetExtent = Ctx.SwapChainExtent;
                }
                else
                {
                    if (p % 2 == 0)
                    {
                        targetImage = Ctx.ScreenPingImage;
                        targetView = Ctx.ScreenPingImageView;
                    }
                    else
                    {
                        targetImage = Ctx.ScreenPongImage;
                        targetView = Ctx.ScreenPongImageView;
                    }

                    targetExtent = Ctx.RenderExtent;

                    TransitionImageLayout(
                        targetImage.Value,
                        ImageLayout.Undefined,
                        ImageLayout.ColorAttachmentOptimal,
                        AccessFlags2.None,
                        AccessFlags2.ColorAttachmentWriteBit,
                        PipelineStageFlags2.ColorAttachmentOutputBit,
                        PipelineStageFlags2.ColorAttachmentOutputBit,
                        ImageAspectFlags.ColorBit
                    );
                }

                var screenAttachmentInfo = new RenderingAttachmentInfo
                {
                    SType = StructureType.RenderingAttachmentInfo,
                    ImageView = targetView,
                    ImageLayout = ImageLayout.ColorAttachmentOptimal,
                    LoadOp = AttachmentLoadOp.Clear,
                    StoreOp = AttachmentStoreOp.Store,
                    ClearValue = clearColor,
                };

                var screenRenderingInfo = new RenderingInfo
                {
                    SType = StructureType.RenderingInfo,
                    RenderArea = new Rect2D(new Offset2D(0, 0), targetExtent),
                    LayerCount = 1,
                    ColorAttachmentCount = 1,
                    PColorAttachments = &screenAttachmentInfo
                };

                Ctx.Vk.CmdBeginRendering(cmdBuffer, &screenRenderingInfo);

                var screenViewport = new Viewport(0.0f, 0.0f, targetExtent.Width, targetExtent.Height, 0.0f, 1.0f);
                Ctx.Vk.CmdSetViewport(cmdBuffer, 0, 1, &screenViewport);

                var screenScissor = new Rect2D(new Offset2D(0, 0), targetExtent);
                Ctx.Vk.CmdSetScissor(cmdBuffer, 0, 1, &screenScissor);

                var materialPipeline = Ctx.ScreenPipelines[p];
                Ctx.Vk.CmdBindPipeline(cmdBuffer, PipelineBindPoint.Graphics, materialPipeline.Pipeline);

                uint[] pushData = [0u, 0u];
                fixed (uint* pPushData = pushData)
                {
                    Ctx.Vk.CmdPushConstants(
                        cmdBuffer,
                        materialPipeline.Layout,
                        ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
                        0,
                        sizeof(uint) * 2,
                        pPushData
                    );
                }

                DescriptorSet lightingSet0 = Ctx.LightingGlobalSetsSet0[Ctx.FrameIndex];
                DescriptorSet screenMaterialSet1 = Ctx.ScreenDescriptorSets[p][Ctx.FrameIndex];
                DescriptorSet[] descriptorSets = [lightingSet0, screenMaterialSet1];

                fixed (DescriptorSet* pDescriptorSets = descriptorSets)
                {
                    Ctx.Vk.CmdBindDescriptorSets(
                        cmdBuffer,
                        PipelineBindPoint.Graphics,
                        materialPipeline.Layout,
                        0,
                        (uint)descriptorSets.Length,
                        pDescriptorSets,
                        0,
                        null
                    );
                }

                Ctx.Vk.CmdDraw(cmdBuffer, 3, 1, 0, 0);

                Ctx.Vk.CmdEndRendering(cmdBuffer);

                if (!isFinalPass && targetImage.HasValue)
                {
                    TransitionImageLayout(
                        targetImage.Value,
                        ImageLayout.ColorAttachmentOptimal,
                        ImageLayout.ShaderReadOnlyOptimal,
                        AccessFlags2.ColorAttachmentWriteBit,
                        AccessFlags2.ShaderReadBit,
                        PipelineStageFlags2.ColorAttachmentOutputBit,
                        PipelineStageFlags2.FragmentShaderBit,
                        ImageAspectFlags.ColorBit
                    );
                }
            }

            TransitionImageLayout(
                srcImage,
                ImageLayout.ShaderReadOnlyOptimal,
                ImageLayout.ColorAttachmentOptimal,
                AccessFlags2.ShaderReadBit,
                AccessFlags2.None,
                PipelineStageFlags2.FragmentShaderBit,
                PipelineStageFlags2.BottomOfPipeBit,
                ImageAspectFlags.ColorBit
            );
        }

        if (isScaling && !hasScreenMaterial)
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