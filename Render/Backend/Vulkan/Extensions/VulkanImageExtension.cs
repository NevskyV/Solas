using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Solas.Render.Vulkan.Extensions;

internal static unsafe class VulkanImageExtension
{
    extension(Image)
    {
        internal static (Image, DeviceMemory) Create(VulkanContext ctx, uint width, uint height, Format format,
            ImageTiling tiling, ImageUsageFlags usage, MemoryPropertyFlags properties)
        {
            ImageCreateInfo imageInfo = new ImageCreateInfo()
            {
                SType = StructureType.ImageCreateInfo,
                ImageType = ImageType.Type2D,
                Format = format,
                Extent = new Extent3D(width, height, 1),
                MipLevels = 1,
                ArrayLayers = 1,
                Samples = SampleCountFlags.Count1Bit,
                Tiling = tiling,
                Usage = usage,
                SharingMode = SharingMode.Exclusive
            };

            if (ctx.Vk!.CreateImage(ctx.Device, &imageInfo, null, out var img) != Result.Success)
            {
                throw new Exception("failed to create image!");
            }

            ctx.Vk!.GetImageMemoryRequirements(ctx.Device, img, out var memRequirements);

            MemoryAllocateInfo allocInfo = new()
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memRequirements.Size,
                MemoryTypeIndex = Buffer.FindMemoryType(ctx, memRequirements.MemoryTypeBits, properties),
            };

            if (ctx.Vk!.AllocateMemory(ctx.Device, in allocInfo, null, out var imageMemoryPtr) != Result.Success)
            {
                throw new Exception("failed to allocate image memory!");
            }

            ctx.Vk!.BindImageMemory(ctx.Device, img, imageMemoryPtr, 0);
            return (img, imageMemoryPtr);
        }
    }
}