using Silk.NET.Vulkan;
using Solas.Render.Vulkan.Extensions;

namespace Solas.Render.Vulkan;

internal class VulkanColorResources : VulkanInjectable, IDisposable
{
    internal void Create()
    {
        Format colorFormat = Ctx.SwapChainSurfaceFormat.Format;

        ImageUsageFlags colorUsage = ImageUsageFlags.ColorAttachmentBit;
        if (Ctx.MsaaSamples == SampleCountFlags.Count1Bit)
        {
            colorUsage |= ImageUsageFlags.TransferSrcBit;
        }

        (Ctx.ColorImage, Ctx.ColorImageMemory) = Image.Create(Ctx, Ctx.RenderExtent.Width,
            Ctx.RenderExtent.Height, 1,
            Ctx.MsaaSamples, colorFormat, ImageTiling.Optimal,
            colorUsage,
            MemoryPropertyFlags.DeviceLocalBit);
        Ctx.ColorImageView = ImageView.Create(Ctx, Ctx.ColorImage, colorFormat, ImageAspectFlags.ColorBit, 1);

        if (Ctx.MsaaSamples != SampleCountFlags.Count1Bit)
        {
            (Ctx.ResolveImage, Ctx.ResolveImageMemory) = Image.Create(Ctx, Ctx.RenderExtent.Width,
                Ctx.RenderExtent.Height, 1,
                SampleCountFlags.Count1Bit, colorFormat, ImageTiling.Optimal,
                ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit,
                MemoryPropertyFlags.DeviceLocalBit);
            Ctx.ResolveImageView = ImageView.Create(Ctx, Ctx.ResolveImage, colorFormat, ImageAspectFlags.ColorBit, 1);
        }
    }

    public unsafe void Dispose()
    {
        Ctx.Vk!.DestroyImageView(Ctx.Device, Ctx.ColorImageView, null);
        Ctx.Vk!.DestroyImage(Ctx.Device, Ctx.ColorImage, null);
        Ctx.Vk!.FreeMemory(Ctx.Device, Ctx.ColorImageMemory, null);

        if (Ctx.ResolveImageView.Handle != 0)
        {
            Ctx.Vk!.DestroyImageView(Ctx.Device, Ctx.ResolveImageView, null);
            Ctx.Vk!.DestroyImage(Ctx.Device, Ctx.ResolveImage, null);
            Ctx.Vk!.FreeMemory(Ctx.Device, Ctx.ResolveImageMemory, null);
            Ctx.ResolveImageView = default;
        }
    }
}