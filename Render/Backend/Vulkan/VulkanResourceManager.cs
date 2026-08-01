using Solas.Render.Components;
using Solas.Render.Vulkan.Components;

namespace Solas.Render.Vulkan;

internal class VulkanResourceManager : VulkanInjectable, IDisposable
{
    private readonly Dictionary<Guid, MeshGpu> _meshes = new();
    private readonly Dictionary<Guid, int> _meshRefCount = new();

    private readonly Dictionary<Guid, TextureGpu> _textures = new();
    private readonly Dictionary<Guid, int> _textureRefCount = new();

    internal MeshGpu AcquireMesh(Mesh meshAsset)
    {
        var id = meshAsset.Id;
        if (_meshes.TryGetValue(id, out var existingMesh))
        {
            _meshRefCount[id]++;
            return existingMesh;
        }

        var newMesh = VulkanMeshLoader.Upload(Ctx, meshAsset);
        meshAsset.FreeCpuData();

        _meshes[id] = newMesh;
        _meshRefCount[id] = 1;
        return newMesh;
    }

    internal void ReleaseMesh(Guid meshId)
    {
        if (!_meshRefCount.ContainsKey(meshId)) return;

        _meshRefCount[meshId]--;
        if (_meshRefCount[meshId] <= 0)
        {
            _meshes[meshId].Dispose();
            _meshes.Remove(meshId);
            _meshRefCount.Remove(meshId);
        }
    }

    internal TextureGpu AcquireTexture(Texture textureAsset)
    {
        var id = textureAsset.Id;
        if (_textures.TryGetValue(id, out var existingTexture))
        {
            _textureRefCount[id]++;
            return existingTexture;
        }

        var newTexture = VulkanTextureLoader.Upload(Ctx, textureAsset);
        textureAsset.FreeCpuData();

        _textures[id] = newTexture;
        _textureRefCount[id] = 1;
        return newTexture;
    }

    internal void ReleaseTexture(Guid textureId)
    {
        if (!_textureRefCount.ContainsKey(textureId)) return;

        _textureRefCount[textureId]--;
        if (_textureRefCount[textureId] <= 0)
        {
            _textures[textureId].Dispose();
            _textures.Remove(textureId);
            _textureRefCount.Remove(textureId);
        }
    }

    public void Dispose()
    {
        foreach (var mesh in _meshes.Values)
        {
            mesh.Dispose();
        }

        _meshes.Clear();
        _meshRefCount.Clear();

        foreach (var texture in _textures.Values)
        {
            texture.Dispose();
        }

        _textures.Clear();
        _textureRefCount.Clear();
    }
}