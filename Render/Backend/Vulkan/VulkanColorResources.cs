using Silk.NET.Vulkan;
using Solas.Render.Vulkan.Extensions;

namespace Solas.Render.Vulkan;

internal class VulkanColorResources : VulkanInjectable
{
    internal void Create()
    {
        Format colorFormat = Ctx.SwapChainSurfaceFormat.Format;

        (Ctx.ColorImage, Ctx.ColorImageMemory) = Image.Create(Ctx, Ctx.SwapChainExtent.Width,
            Ctx.SwapChainExtent.Height, 1,
            Ctx.MsaaSamples, colorFormat, ImageTiling.Optimal,
            ImageUsageFlags.TransientAttachmentBit | ImageUsageFlags.ColorAttachmentBit,
            MemoryPropertyFlags.DeviceLocalBit);
        Ctx.ColorImageView = ImageView.Create(Ctx, Ctx.ColorImage, colorFormat, ImageAspectFlags.ColorBit, 1);
    }
}