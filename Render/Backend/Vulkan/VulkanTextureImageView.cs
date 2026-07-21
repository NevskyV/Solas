using Silk.NET.Vulkan;
using Solas.Render.Vulkan.Extensions;

namespace Solas.Render.Vulkan;

internal class VulkanTextureImageView : VulkanInjectable
{
    internal void Create()
    {
        Ctx.TextureImageView = ImageView.Create(Ctx, Ctx.TextureImage, Format.R8G8B8A8Srgb, ImageAspectFlags.ColorBit);
    }
}