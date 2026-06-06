using Solas.Assets;
using Solas.Components;
using Solas.Interfaces;
using Solas.Serialization;
using Solas.Settings;
using Solas.World;

namespace Solas.Containers;

internal class AssetsPool
{
    private readonly List<Asset> _createdAssets = [];
    private readonly List<Asset> _loadedAssets = [];
    private Dictionary<Guid, uint> _assetsPointers;
    private Dictionary<Guid, uint> _entitiesPointers;
    
    private readonly CoreSettings _coreSettings = WorldContext.CoreSettings;
    private readonly Space _globalSpace = WorldContext.GlobalSpace;

    internal void ReadPointers()
    {
        _assetsPointers = SearchIdSerializer.ReadAll(_coreSettings.AssetsPackPath + ".lookup");
        _entitiesPointers = SearchIdSerializer.ReadAll(_coreSettings.AssetsSpacePath + ".lookup");
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
        if (!_loadedAssets.Exists(x => x.Id == id))
        {
            return LoadAsset<T>(id);
        }
        return (T)_loadedAssets.Find(x => x.Id == id);
    }

    internal void SaveNewAssets()
    {
        using var stream = File.Open(_coreSettings.AssetsPackPath, FileMode.Append, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        foreach (var asset in _createdAssets)
        {
            SearchIdSerializer.Write(_coreSettings.AssetsPackPath + ".lookup", asset.Id, (uint)stream.Position);
        
            writer.Write(asset.Id.ToByteArray());
            asset.Write(writer);
        }
    }
    
    internal void SaveAsset(Asset asset)
    {
        using var stream = File.Open(_coreSettings.AssetsPackPath, FileMode.Append, FileAccess.Write);
        using var writer = new BinaryWriter(stream);
        
        SearchIdSerializer.Write(_coreSettings.AssetsPackPath + ".lookup", asset.Id, (uint)stream.Position);
        
        writer.Write(asset.Id.ToByteArray());
        asset.Write(writer);
    }

    internal T LoadAsset<T>(Guid id) where T : IReferenceable, new()
    {
        using var stream = File.Open(_coreSettings.AssetsPackPath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream);
        
        stream.Position = _assetsPointers[id];
        var asset = (T)new T().Read(reader);
        _loadedAssets.Add(asset as Asset);
        return asset;
    }

    internal Entity LoadEntity(Guid id)
    {
        using var stream = File.Open(_coreSettings.AssetsSpacePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream);
        
        stream.Position = _entitiesPointers[id];
        var entity = Entity.StaticRead(reader, _globalSpace);
        EngineContext.DISystem.BuildDependencies(_globalSpace);
        return entity;
    }
}