using Silk.NET.Vulkan;

namespace Solas.Render.Vulkan.Extensions;

public static unsafe class VulkanImageViewExtension
{
    extension(ImageView)
    {
        internal static ImageView Create(VulkanContext ctx, Image image, Format format)
        {
            var imageViewCreateInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = image,
                ViewType = ImageViewType.Type2D,
                Format = format,
                SubresourceRange =
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };
            var result = ctx.Vk!.CreateImageView(ctx.Device, &imageViewCreateInfo, null, out var imageView);
            return result != Result.Success ? throw new Exception($"Failed to create image view: {result}") : imageView;
        }
    }
}