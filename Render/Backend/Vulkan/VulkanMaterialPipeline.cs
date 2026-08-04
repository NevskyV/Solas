using Silk.NET.Vulkan;

namespace Solas.Render.Vulkan;

internal struct VulkanMaterialPipeline
{
    internal Pipeline Pipeline;
    internal PipelineLayout Layout;
    internal string Hash;

    internal unsafe void Dispose(Vk vk, Device device)
    {
        if (Pipeline.Handle != 0)
        {
            vk.DestroyPipeline(device, Pipeline, null);
            Pipeline = default;
        }

        if (Layout.Handle != 0)
        {
            vk.DestroyPipelineLayout(device, Layout, null);
            Layout = default;
        }
    }
}
