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

        void* data;
        Ctx.Vk!.MapMemory(Ctx.Device, stagingBufferMemory, 0, imageSize, 0, &data);
        image.Data.AsSpan().CopyTo(new Span<byte>(data, (int)imageSize));
        Ctx.Vk!.UnmapMemory(Ctx.Device, stagingBufferMemory);

        (Ctx.TextureImage, Ctx.TextureImageMemory) = Image.Create(
            Ctx,
            image.Width,
            image.Height,
            Format.R8G8B8A8Srgb,
            ImageTiling.Optimal,
            ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
            MemoryPropertyFlags.DeviceLocalBit);

        CommandBuffer commandBuffer = Buffer.BeginSingleTimeCommands(Ctx);
        TransitionImageLayout(Ctx, commandBuffer, Ctx.TextureImage, ImageLayout.Undefined,
            ImageLayout.TransferDstOptimal);
        CopyBufferToImage(commandBuffer, stagingBuffer, Ctx.TextureImage, image.Width, image.Height);
        TransitionImageLayout(Ctx, commandBuffer, Ctx.TextureImage, ImageLayout.TransferDstOptimal,
            ImageLayout.ShaderReadOnlyOptimal);
        Buffer.EndSingleTimeCommands(Ctx, commandBuffer);

        Ctx.Vk!.DestroyBuffer(Ctx.Device, stagingBuffer, null);
        Ctx.Vk!.FreeMemory(Ctx.Device, stagingBufferMemory, null);
    }

    private void TransitionImageLayout(VulkanContext ctx, CommandBuffer commandBuffer, Image image,
        ImageLayout oldLayout, ImageLayout newLayout)
    {
        ImageMemoryBarrier barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = { AspectMask = ImageAspectFlags.ColorBit, LevelCount = 1, LayerCount = 1 }
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

        ctx.Vk!.CmdPipelineBarrier(commandBuffer, sourceStage, destinationStage, 0, 0,
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