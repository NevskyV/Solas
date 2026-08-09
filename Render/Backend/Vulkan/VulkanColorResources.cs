using Silk.NET.Vulkan;
using Solas.Render.Vulkan.Extensions;

namespace Solas.Render.Vulkan;

internal class VulkanColorResources : VulkanInjectable, IDisposable
{
    internal unsafe void Create()
    {
        Format colorFormat = Ctx.SwapChainSurfaceFormat.Format;

        ImageUsageFlags colorUsage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit;
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
                ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.SampledBit,
                MemoryPropertyFlags.DeviceLocalBit);
            Ctx.ResolveImageView = ImageView.Create(Ctx, Ctx.ResolveImage, colorFormat, ImageAspectFlags.ColorBit, 1);
        }

        Ctx.Vk!.GetPhysicalDeviceProperties(Ctx.PhysicalDevice, out var pProperties);
        SamplerCreateInfo samplerInfo = new()
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            MipmapMode = SamplerMipmapMode.Linear,
            MipLodBias = 0f,
            MinLod = 0f,
            MaxLod = Vk.LodClampNone,
            AddressModeU = SamplerAddressMode.ClampToEdge,
            AddressModeV = SamplerAddressMode.ClampToEdge,
            AddressModeW = SamplerAddressMode.ClampToEdge,
            AnisotropyEnable = false,
            CompareEnable = false,
            CompareOp = CompareOp.Always,
            BorderColor = BorderColor.FloatOpaqueBlack,
            UnnormalizedCoordinates = false
        };

        if (Ctx.Vk!.CreateSampler(Ctx.Device, in samplerInfo, null, out Ctx.ScreenSampler) != Result.Success)
        {
            throw new Exception("failed to create screen sampler!");
        }
    }

    public unsafe void Dispose()
    {
        if (Ctx.ScreenSampler.Handle != 0)
        {
            Ctx.Vk!.DestroySampler(Ctx.Device, Ctx.ScreenSampler, null);
            Ctx.ScreenSampler = default;
        }

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