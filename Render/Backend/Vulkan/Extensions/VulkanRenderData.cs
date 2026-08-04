using Silk.NET.Vulkan;
using Solas.Render.Components;
using Solas.Render.Logics;
using Solas.Render.Vulkan.Components;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Solas.Render.Vulkan.Extensions;

internal class VulkanRenderData
{
    internal MeshRenderLogic Logic { get; }
    internal MeshGpu? GpuMesh = null!;
    internal TextureGpu? GpuTexture = null!;
    internal Material? Material = null;
    internal VulkanMaterialPipeline MaterialPipeline;

    internal Buffer[]? UniformBuffers = null;
    internal DeviceMemory[] UniformBuffersMemory = null!;
    internal DescriptorSet[] DescriptorSets = null!;

    internal VulkanRenderData(MeshRenderLogic logic)
    {
        Logic = logic;
        Material = logic.Material;
    }
}