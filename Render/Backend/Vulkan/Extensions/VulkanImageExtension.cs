using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Solas.Render.Vulkan.Extensions;

internal static unsafe class VulkanImageExtension
{
    extension(Image)
    {
        internal static (Image, DeviceMemory) Create(VulkanContext ctx, uint width, uint height, uint mipLevels,
            SampleCountFlags numSamples, Format format,
            ImageTiling tiling, ImageUsageFlags usage, MemoryPropertyFlags properties)
        {
            return Create(ctx, width, height, mipLevels, 1, numSamples, format, tiling, usage, properties,
                ImageCreateFlags.None);
        }

        internal static (Image, DeviceMemory) Create(VulkanContext ctx, uint width, uint height, uint mipLevels,
            uint arrayLayers, SampleCountFlags numSamples, Format format,
            ImageTiling tiling, ImageUsageFlags usage, MemoryPropertyFlags properties, ImageCreateFlags flags)
        {
            var imageInfo = new ImageCreateInfo
            {
                SType = StructureType.ImageCreateInfo,
                Flags = flags,
                ImageType = ImageType.Type2D,
                Format = format,
                Extent = new Extent3D(width, height, 1),
                MipLevels = mipLevels,
                ArrayLayers = arrayLayers,
                Samples = numSamples,
                Tiling = tiling,
                Usage = usage,
                SharingMode = SharingMode.Exclusive,
                InitialLayout = ImageLayout.Undefined
            };

            if (ctx.Vk!.CreateImage(ctx.Device, &imageInfo, null, out var image) != Result.Success)
            {
                throw new InvalidOperationException("Failed to create Vulkan image.");
            }

            ctx.Vk.GetImageMemoryRequirements(ctx.Device, image, out var memoryRequirements);

            var allocationInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memoryRequirements.Size,
                MemoryTypeIndex = Buffer.FindMemoryType(ctx, memoryRequirements.MemoryTypeBits, properties)
            };

            if (ctx.Vk.AllocateMemory(ctx.Device, in allocationInfo, null, out var imageMemory) != Result.Success)
            {
                ctx.Vk.DestroyImage(ctx.Device, image, null);
                throw new InvalidOperationException("Failed to allocate Vulkan image memory.");
            }

            if (ctx.Vk.BindImageMemory(ctx.Device, image, imageMemory, 0) != Result.Success)
            {
                ctx.Vk.FreeMemory(ctx.Device, imageMemory, null);
                ctx.Vk.DestroyImage(ctx.Device, image, null);
                throw new InvalidOperationException("Failed to bind Vulkan image memory.");
            }

            return (image, imageMemory);
        }
    }
}