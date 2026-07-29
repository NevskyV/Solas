using Silk.NET.Vulkan;
using Solas.Render.Components;
using Solas.Render.Vulkan.Extensions;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanTextureImage : VulkanInjectable
{
    internal void Create()
    {
        var image = new Texture(Ctx.TexturePath);
        var imageSize = (ulong)(image.Width * image.Height * 4);
        var (stagingBuffer, stagingBufferMemory) =
            Buffer.Create(Ctx, imageSize, BufferUsageFlags.TransferSrcBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        Ctx.MipLevels = image.MipLevels;
        //TODO: remove to individuals

        void* data;
        Ctx.Vk!.MapMemory(Ctx.Device, stagingBufferMemory, 0, imageSize, 0, &data);
        image.Data.AsSpan().CopyTo(new Span<byte>(data, (int)imageSize));
        Ctx.Vk!.UnmapMemory(Ctx.Device, stagingBufferMemory);

        (Ctx.TextureImage, Ctx.TextureImageMemory) = Image.Create(
            Ctx,
            image.Width,
            image.Height,
            image.MipLevels,
            SampleCountFlags.Count1Bit,
            Format.R8G8B8A8Srgb,
            ImageTiling.Optimal,
            ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
            MemoryPropertyFlags.DeviceLocalBit);

        CommandBuffer commandBuffer = Buffer.BeginSingleTimeCommands(Ctx);
        TransitionImageLayout(commandBuffer, Ctx.TextureImage, ImageLayout.Undefined,
            ImageLayout.TransferDstOptimal, image.MipLevels);
        CopyBufferToImage(commandBuffer, stagingBuffer, Ctx.TextureImage, image.Width, image.Height);
        GenerateMipMaps(commandBuffer, Ctx.TextureImage, Format.R8G8B8A8Srgb, (int)image.Width, (int)image.Height,
            image.MipLevels);
        Buffer.EndSingleTimeCommands(Ctx, commandBuffer);

        Ctx.Vk!.DestroyBuffer(Ctx.Device, stagingBuffer, null);
        Ctx.Vk!.FreeMemory(Ctx.Device, stagingBufferMemory, null);
    }

    private void GenerateMipMaps(CommandBuffer commandBuffer, Image image, Format imageFormat, int width, int height,
        uint mipLevels)
    {
        var formatProperties = Ctx.Vk!.GetPhysicalDeviceFormatProperties(Ctx.PhysicalDevice, imageFormat);

        if ((formatProperties.OptimalTilingFeatures & FormatFeatureFlags.SampledImageFilterLinearBit) == 0)
        {
            throw new Exception("texture image format does not support linear blitting!");
        }

        ImageMemoryBarrier barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = AccessFlags.TransferWriteBit,
            DstAccessMask = AccessFlags.TransferReadBit,
            OldLayout = ImageLayout.TransferDstOptimal,
            NewLayout = ImageLayout.TransferSrcOptimal,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange =
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseArrayLayer = 0,
                LayerCount = 1,
                LevelCount = 1,
            }
        };

        int mipWidth = width;
        int mipHeight = height;

        for (uint i = 1; i < mipLevels; i++)
        {
            barrier.SubresourceRange.BaseMipLevel = i - 1;
            barrier.OldLayout = ImageLayout.TransferDstOptimal;
            barrier.NewLayout = ImageLayout.TransferSrcOptimal;
            barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
            barrier.DstAccessMask = AccessFlags.TransferReadBit;

            Ctx.Vk!.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TransferBit, PipelineStageFlags.TransferBit,
                0, 0, null, 0, null,
                1, in barrier);

            ImageBlit blit = new()
            {
                SrcSubresource = { AspectMask = ImageAspectFlags.ColorBit, MipLevel = i - 1, LayerCount = 1 },
                SrcOffsets = { Element0 = { }, Element1 = new Offset3D(mipWidth, mipHeight, 1) },
                DstSubresource = { AspectMask = ImageAspectFlags.ColorBit, MipLevel = i, LayerCount = 1 },
                DstOffsets =
                {
                    Element0 = { },
                    Element1 = new Offset3D(1 < mipWidth ? mipWidth / 2 : 1, 1 < mipHeight ? mipHeight / 2 : 1, 1)
                }
            };

            Ctx.Vk!.CmdBlitImage(commandBuffer, image, ImageLayout.TransferSrcOptimal, image,
                ImageLayout.TransferDstOptimal, 1, in blit, Filter.Linear);

            barrier.OldLayout = ImageLayout.TransferSrcOptimal;
            barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
            barrier.SrcAccessMask = AccessFlags.TransferReadBit;
            barrier.DstAccessMask = AccessFlags.ShaderReadBit;

            Ctx.Vk!.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TransferBit,
                PipelineStageFlags.FragmentShaderBit,
                0, 0, null, 0, null,
                1, in barrier);

            if (1 < mipWidth)
            {
                mipWidth /= 2;
            }

            if (1 < mipHeight)
            {
                mipHeight /= 2;
            }
        }

        barrier.SubresourceRange.BaseMipLevel = mipLevels - 1;
        barrier.OldLayout = ImageLayout.TransferDstOptimal;
        barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
        barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
        barrier.DstAccessMask = AccessFlags.ShaderReadBit;

        Ctx.Vk!.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit,
            0, 0, null, 0, null,
            1, in barrier);
    }

    private void TransitionImageLayout(CommandBuffer commandBuffer, Image image,
        ImageLayout oldLayout, ImageLayout newLayout, uint mipLevels)
    {
        ImageMemoryBarrier barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = { AspectMask = ImageAspectFlags.ColorBit, LevelCount = mipLevels, LayerCount = 1 }
        };

        PipelineStageFlags sourceStage;
        PipelineStageFlags destinationStage;

        if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
        {
            barrier.SrcAccessMask = 0;
            barrier.DstAccessMask = AccessFlags.TransferWriteBit;

            sourceStage = PipelineStageFlags.TopOfPipeBit;
            destinationStage = PipelineStageFlags.TransferBit;
        }
        else if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.ShaderReadOnlyOptimal)
        {
            barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
            barrier.DstAccessMask = AccessFlags.ShaderReadBit;

            sourceStage = PipelineStageFlags.TransferBit;
            destinationStage = PipelineStageFlags.FragmentShaderBit;
        }
        else
        {
            throw new Exception("unsupported layout transition!");
        }

        Ctx.Vk!.CmdPipelineBarrier(commandBuffer, sourceStage, destinationStage, 0, 0,
            null, 0, null, 1, in barrier);
    }

    internal void CopyBufferToImage(CommandBuffer commandBuffer, Buffer buffer, Image image, uint width, uint height)
    {
        BufferImageCopy region = new()
        {
            BufferOffset = 0,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource =
                { AspectMask = ImageAspectFlags.ColorBit, MipLevel = 0, BaseArrayLayer = 0, LayerCount = 1 },
            ImageOffset = { X = 0, Y = 0, Z = 0 },
            ImageExtent = { Width = width, Height = height, Depth = 1 },
        };

        Ctx.Vk!.CmdCopyBufferToImage(commandBuffer, buffer, image, ImageLayout.TransferDstOptimal, 1, in region);
    }
}