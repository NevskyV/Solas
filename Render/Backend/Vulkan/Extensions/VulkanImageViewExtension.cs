using Silk.NET.Vulkan;

namespace Solas.Render.Vulkan.Extensions;

public static unsafe class VulkanImageViewExtension
{
    extension(ImageView)
    {
        internal static ImageView Create(VulkanContext ctx, Image image, Format format, ImageAspectFlags aspectFlags,
            uint mipLevels)
        {
            return Create(ctx, image, format, aspectFlags, mipLevels, ImageViewType.Type2D, 0, 1);
        }

        internal static ImageView Create(VulkanContext ctx, Image image, Format format, ImageAspectFlags aspectFlags,
            uint mipLevels, ImageViewType viewType, uint baseArrayLayer, uint layerCount)
        {
            var imageViewCreateInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = image,
                ViewType = viewType,
                Format = format,
                SubresourceRange =
                {
                    AspectMask = aspectFlags,
                    BaseMipLevel = 0,
                    LevelCount = mipLevels,
                    BaseArrayLayer = baseArrayLayer,
                    LayerCount = layerCount
                }
            };

            var result = ctx.Vk!.CreateImageView(ctx.Device, &imageViewCreateInfo, null, out var imageView);
            return result != Result.Success
                ? throw new InvalidOperationException($"Failed to create Vulkan image view: {result}")
                : imageView;
        }
    }
}