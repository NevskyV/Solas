namespace Solas.Render.Vulkan;

internal abstract class VulkanInjectable
{
    internal static VulkanContext Ctx { get; set; } = null!;
}