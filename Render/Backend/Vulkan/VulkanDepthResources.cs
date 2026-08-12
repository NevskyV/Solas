using Silk.NET.Vulkan;
using Solas.Render.Vulkan.Extensions;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanDepthResources : VulkanInjectable, IDisposable
{
    internal void Create()
    {
        Ctx.DepthFormat = FindDepthFormat();
        (Ctx.DepthImage, Ctx.DepthImageMemory) = Image.Create(
            Ctx,
            Ctx.RenderExtent.Width,
            Ctx.RenderExtent.Height,
            1,
            Ctx.MsaaSamples,
            Ctx.DepthFormat,
            ImageTiling.Optimal,
            ImageUsageFlags.DepthStencilAttachmentBit,
            MemoryPropertyFlags.DeviceLocalBit);

        const ImageAspectFlags aspectFlags = ImageAspectFlags.DepthBit | ImageAspectFlags.StencilBit;
        Ctx.DepthImageView = ImageView.Create(Ctx, Ctx.DepthImage, Ctx.DepthFormat, aspectFlags, 1);
    }

    private Format FindDepthFormat()
    {
        return FindSupportedFormat(
            [Format.D24UnormS8Uint, Format.D32SfloatS8Uint, Format.D16UnormS8Uint, Format.D32Sfloat],
            ImageTiling.Optimal, FormatFeatureFlags.DepthStencilAttachmentBit);
    }

    private Format FindSupportedFormat(Format[] candidates, ImageTiling tiling, FormatFeatureFlags features)
    {
        foreach (var format in candidates)
        {
            var props = Ctx.Vk!.GetPhysicalDeviceFormatProperties(Ctx.PhysicalDevice, format);
            if (((tiling == ImageTiling.Linear) && ((props.LinearTilingFeatures & features) == features)) ||
                ((tiling == ImageTiling.Optimal) && ((props.OptimalTilingFeatures & features) == features)))
            {
                return format;
            }
        }

        throw new Exception("failed to find supported format!");
    }

    public void Dispose()
    {
        Ctx.Vk!.DestroyImage(Ctx.Device, Ctx.DepthImage, null);
        Ctx.Vk!.DestroyImageView(Ctx.Device, Ctx.DepthImageView, null);
        Ctx.Vk!.FreeMemory(Ctx.Device, Ctx.DepthImageMemory, null);
    }
}