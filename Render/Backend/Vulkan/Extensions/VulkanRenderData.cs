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
    internal Material? Material = null;
    internal VulkanMaterialPipeline MaterialPipeline;

    internal Dictionary<int, TextureGpu> BoundGpuTextures = new();
    private readonly Dictionary<int, Guid> _boundTextureIds = new();

    internal Buffer[]? UniformBuffers = null;
    internal DeviceMemory[] UniformBuffersMemory = null!;
    internal DescriptorSet[] DescriptorSets = null!;

    internal VulkanRenderData(MeshRenderLogic logic)
    {
        Logic = logic;
        Material = logic.Material;
    }

    internal void AcquireGpuTextures(VulkanResourceManager resourceManager)
    {
        if (Material == null) return;

        foreach (var binding in Material.GetAllTextureBindings())
        {
            int bindingIdx = binding.BindingIndex;
            if (binding.Texture != null)
            {
                var newId = binding.Texture.Id;
                if (_boundTextureIds.TryGetValue(bindingIdx, out var oldId))
                {
                    if (oldId != newId)
                    {
                        resourceManager.ReleaseTexture(oldId);
                    }
                }

                var gpuTex = resourceManager.AcquireTexture(binding.Texture);
                BoundGpuTextures[bindingIdx] = gpuTex;
                _boundTextureIds[bindingIdx] = newId;
            }
            else
            {
                if (_boundTextureIds.TryGetValue(bindingIdx, out var oldId))
                {
                    resourceManager.ReleaseTexture(oldId);
                    _boundTextureIds.Remove(bindingIdx);
                }

                BoundGpuTextures.Remove(bindingIdx);
            }
        }
    }

    internal void ReleaseGpuTextures(VulkanResourceManager resourceManager)
    {
        foreach (var id in _boundTextureIds.Values)
        {
            resourceManager.ReleaseTexture(id);
        }

        _boundTextureIds.Clear();
        BoundGpuTextures.Clear();
    }
}