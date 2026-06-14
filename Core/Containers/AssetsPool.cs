using Solas.Assets;
using Solas.Components;
using Solas.Interfaces;
using Solas.Serialization;
using Solas.Serialization.Binary;
using Solas.Serialization.Core;
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

        _assetsPointers = IdLookupSerializer.ReadAll(CoreSettings.AssetsPackPath + ".lookup");
        _entitiesPointers = IdLookupSerializer.ReadAll(CoreSettings.AssetsSpacePath + ".lookup");
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

    private void WriteAsset(Asset asset, FileStream stream, BinaryWriter binaryWriter)
    {
        IdLookupSerializer.Write(binaryWriter, asset.Id, (uint)stream.Position);
        EngineContext.Serializer.Write(asset, stream);
    }

    internal void SaveNewAssets()
    {
        using var stream = File.Open(CoreSettings.AssetsPackPath, FileMode.Append, FileAccess.Write);
        EngineContext.Serializer.Open(stream);
        BinaryWriter binaryWriter = new BinaryWriter(File.Open(CoreSettings.AssetsPackPath + ".lookup", FileMode.Append, FileAccess.Write));
        
        foreach (var asset in _createdAssets)
            WriteAsset(asset, stream, binaryWriter);
        
        EngineContext.Serializer.Close(stream);
    }

    internal void SaveAsset(Asset asset)
    {
        using var stream = File.Open(CoreSettings.AssetsPackPath, FileMode.Append, FileAccess.Write);
        EngineContext.Serializer.Open(stream);
        BinaryWriter binaryWriter = new BinaryWriter(File.Open(CoreSettings.AssetsPackPath + ".lookup", FileMode.Append, FileAccess.Write));

        WriteAsset(asset, stream, binaryWriter);
        EngineContext.Serializer.Close(stream);
    }

    internal T LoadAsset<T>(Guid id) where T : IReferenceable, new()
    {
        using var stream = File.Open(CoreSettings.AssetsPackPath, FileMode.Open, FileAccess.Read);
        EngineContext.Serializer.Open(stream);
        stream.Position = _assetsPointers[id];
        var asset = EngineContext.Serializer.Read<T>(stream);
        _loadedAssets.Add(asset as Asset);
        
        EngineContext.Serializer.Close(stream);
        return asset;
    }

    internal Entity LoadEntity(Guid id)
    {
        using var stream = File.Open(CoreSettings.AssetsSpacePath, FileMode.Open, FileAccess.Read);
        EngineContext.Serializer.Open(stream);
        stream.Position = _entitiesPointers[id];
        var entity = EngineContext.Serializer.Read<Entity>(stream);

        EngineContext.DISystem.BuildDependencies(WorldContext.GlobalSpace);
        
        EngineContext.Serializer.Close(stream);
        return entity;
    }
}