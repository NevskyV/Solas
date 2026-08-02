using Solas.Render.Vulkan.Components;
using Solas.Render.Vulkan.Extensions;
using Silk.NET.Vulkan;
using Solas.Render.Components;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Solas.Render.Vulkan;

internal static unsafe class VulkanTextureLoader
{
    internal static TextureGpu Upload(VulkanContext ctx, Texture texture)
    {
        var imageSize = (ulong)(texture.Width * texture.Height * 4);
        var (stagingBuffer, stagingMemory) = Buffer.Create(ctx, imageSize, BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        void* data;
        ctx.Vk!.MapMemory(ctx.Device, stagingMemory, 0, imageSize, 0, &data);
        texture.Data.AsSpan().CopyTo(new Span<byte>(data, (int)imageSize));
        ctx.Vk!.UnmapMemory(ctx.Device, stagingMemory);

        var (image, memory) = Image.Create(
            ctx,
            texture.Width,
            texture.Height,
            texture.MipLevels,
            SampleCountFlags.Count1Bit,
            Format.R8G8B8A8Srgb,
            ImageTiling.Optimal,
            ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
            MemoryPropertyFlags.DeviceLocalBit);

        CommandBuffer cmd = Buffer.BeginSingleTimeCommands(ctx);
        TransitionLayout(ctx, cmd, image, ImageLayout.Undefined, ImageLayout.TransferDstOptimal, texture.MipLevels);
        CopyBufferToImage(ctx, cmd, stagingBuffer, image, texture.Width, texture.Height);
        GenerateMipMaps(ctx, cmd, image, Format.R8G8B8A8Srgb, (int)texture.Width, (int)texture.Height,
            texture.MipLevels);
        Buffer.EndSingleTimeCommands(ctx, cmd);

        ctx.Vk!.DestroyBuffer(ctx.Device, stagingBuffer, null);
        ctx.Vk!.FreeMemory(ctx.Device, stagingMemory, null);

        var imageView = ImageView.Create(ctx, image, Format.R8G8B8A8Srgb, ImageAspectFlags.ColorBit, texture.MipLevels);
        var sampler = CreateSampler(ctx);

        return new TextureGpu(ctx.Vk, ctx.Device, image, memory, imageView, sampler);
    }

    private static Sampler CreateSampler(VulkanContext ctx)
    {
        ctx.Vk!.GetPhysicalDeviceProperties(ctx.PhysicalDevice, out var pProperties);
        SamplerCreateInfo samplerInfo = new()
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            MipmapMode = SamplerMipmapMode.Linear,
            MipLodBias = 0f,
            MinLod = 0f,
            MaxLod = Vk.LodClampNone,
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            AnisotropyEnable = true,
            MaxAnisotropy = Math.Min(pProperties.Limits.MaxSamplerAnisotropy, ctx.Settings.AnisotropyLevel),
            CompareEnable = false,
            CompareOp = CompareOp.Always,
            BorderColor = BorderColor.FloatOpaqueBlack,
            UnnormalizedCoordinates = false
        };

        Sampler sampler;
        ctx.Vk!.CreateSampler(ctx.Device, &samplerInfo, null, out sampler);
        return sampler;
    }

    private static void CopyBufferToImage(VulkanContext ctx, CommandBuffer cmd, Buffer buffer, Image image, uint width,
        uint height)
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

        ctx.Vk!.CmdCopyBufferToImage(cmd, buffer, image, ImageLayout.TransferDstOptimal, 1, in region);
    }

    private static void TransitionLayout(VulkanContext ctx, CommandBuffer cmd, Image image, ImageLayout oldLayout,
        ImageLayout newLayout, uint mipLevels)
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

        PipelineStageFlags srcStage;
        PipelineStageFlags dstStage;

        if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
        {
            barrier.SrcAccessMask = 0;
            barrier.DstAccessMask = AccessFlags.TransferWriteBit;
            srcStage = PipelineStageFlags.TopOfPipeBit;
            dstStage = PipelineStageFlags.TransferBit;
        }
        else
        {
            throw new Exception("unsupported layout transition!");
        }

        ctx.Vk!.CmdPipelineBarrier(cmd, srcStage, dstStage, 0, 0, null, 0, null, 1, in barrier);
    }

    private static void GenerateMipMaps(VulkanContext ctx, CommandBuffer cmd, Image image, Format format, int width,
        int height, uint mipLevels)
    {
        var formatProperties = ctx.Vk!.GetPhysicalDeviceFormatProperties(ctx.PhysicalDevice, format);
        if ((formatProperties.OptimalTilingFeatures & FormatFeatureFlags.SampledImageFilterLinearBit) == 0)
        {
            throw new Exception("texture format does not support linear blitting!");
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
                { AspectMask = ImageAspectFlags.ColorBit, BaseArrayLayer = 0, LayerCount = 1, LevelCount = 1 }
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

            ctx.Vk!.CmdPipelineBarrier(cmd, PipelineStageFlags.TransferBit, PipelineStageFlags.TransferBit, 0, 0, null,
                0, null, 1, in barrier);

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

            ctx.Vk!.CmdBlitImage(cmd, image, ImageLayout.TransferSrcOptimal, image, ImageLayout.TransferDstOptimal, 1,
                in blit, Filter.Linear);

            barrier.OldLayout = ImageLayout.TransferSrcOptimal;
            barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
            barrier.SrcAccessMask = AccessFlags.TransferReadBit;
            barrier.DstAccessMask = AccessFlags.ShaderReadBit;

            ctx.Vk!.CmdPipelineBarrier(cmd, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit, 0, 0,
                null, 0, null, 1, in barrier);

            if (1 < mipWidth) mipWidth /= 2;
            if (1 < mipHeight) mipHeight /= 2;
        }

        barrier.SubresourceRange.BaseMipLevel = mipLevels - 1;
        barrier.OldLayout = ImageLayout.TransferDstOptimal;
        barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
        barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
        barrier.DstAccessMask = AccessFlags.ShaderReadBit;

        ctx.Vk!.CmdPipelineBarrier(cmd, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit, 0, 0,
            null, 0, null, 1, in barrier);
    }
}