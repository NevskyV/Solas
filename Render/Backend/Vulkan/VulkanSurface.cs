using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Solas.Render.Backend.Vulkan;

internal unsafe class VulkanSurface : VulkanInjectable
{
    internal void CreateSurface()
    {
        if (!Ctx.Vk!.TryGetInstanceExtension<KhrSurface>(Ctx.Instance, out Ctx.KhrSurface))
        {
            throw new NotSupportedException("KHR_surface extension not found.");
        }

        Ctx.Surface = Ctx.Window!.VkSurface!.Create<AllocationCallbacks>(Ctx.Instance.ToHandle(), null).ToSurface();
    }
}