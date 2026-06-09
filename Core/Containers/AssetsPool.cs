using Solas.Assets;
using Solas.Components;
using Solas.Interfaces;
using Solas.Serialization;
using Solas.Settings;

namespace Solas.Containers;

internal class AssetsPool
{
    private readonly List<Asset> _createdAssets = [];
    private readonly List<Asset> _loadedAssets = [];
    private Dictionary<Guid, uint> _assetsPointers;
    private Dictionary<Guid, uint> _entitiesPointers;

    private CoreSettings CoreSettings => WorldContext.CoreSettings;

    internal void ReadPointers()
    {
        if (!File.Exists(CoreSettings.AssetsPackPath)) File.Create(CoreSettings.AssetsPackPath).Close();
        if (!File.Exists(CoreSettings.AssetsSpacePath)) File.Create(CoreSettings.AssetsSpacePath).Close();

        _assetsPointers = SearchIdSerializer.ReadAll(CoreSettings.AssetsPackPath + ".lookup");
        _entitiesPointers = SearchIdSerializer.ReadAll(CoreSettings.AssetsSpacePath + ".lookup");
    }

    internal void RegisterNewAsset(Asset asset)
    {
        _createdAssets.Add(asset);
    }

    internal Asset GetLoadedAsset(Guid id)
    {
        var res = _loadedAssets.Find(x => x.Id == id);
        return res;
    }

    internal T GetAsset<T>(Guid id) where T : Asset, new()
    {
        if (!_loadedAssets.Exists(x => x.Id == id)) return LoadAsset<T>(id);
        return (T)_loadedAssets.Find(x => x.Id == id);
    }

    internal void SaveNewAssets()
    {
        using var stream = File.Open(CoreSettings.AssetsPackPath, FileMode.Append, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        foreach (var asset in _createdAssets)
        {
            SearchIdSerializer.Write(CoreSettings.AssetsPackPath + ".lookup", asset.Id, (uint)stream.Position);

            asset.Write(writer);
        }
    }

    internal void SaveAsset(Asset asset)
    {
        using var stream = File.Open(CoreSettings.AssetsPackPath, FileMode.Append, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        SearchIdSerializer.Write(CoreSettings.AssetsPackPath + ".lookup", asset.Id, (uint)stream.Position);

        asset.Write(writer);
    }

    internal T LoadAsset<T>(Guid id) where T : IReferenceable, new()
    {
        using var stream = File.Open(CoreSettings.AssetsPackPath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream);

        stream.Position = _assetsPointers[id];
        var asset = (T)new T { Id = id }.Read(reader);
        _loadedAssets.Add(asset as Asset);
        return asset;
    }

    internal Entity LoadEntity(Guid id)
    {
        using var stream = File.Open(CoreSettings.AssetsSpacePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream);

        stream.Position = _entitiesPointers[id];
        var entity = Entity.StaticRead(reader, WorldContext.GlobalSpace);
        EngineContext.DISystem.BuildDependencies(WorldContext.GlobalSpace);
        return entity;
    }
}