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
            Ctx.SwapChainExtent.Width,
            Ctx.SwapChainExtent.Height,
            Ctx.DepthFormat,
            ImageTiling.Optimal,
            ImageUsageFlags.DepthStencilAttachmentBit,
            MemoryPropertyFlags.DeviceLocalBit);

        Ctx.DepthImageView = ImageView.Create(Ctx, Ctx.DepthImage, Ctx.DepthFormat, ImageAspectFlags.DepthBit);
    }

    private Format FindDepthFormat()
    {
        return FindSupportedFormat([Format.D32Sfloat, Format.D32SfloatS8Uint, Format.D24UnormS8Uint],
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