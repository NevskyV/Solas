using Solas.Assets;
using Solas.Components;
using Solas.Interfaces;
using Solas.Serialization.Binary;
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
        var assetsPackPath = EngineContext.Vfs.GetPath(CoreSettings.AssetsPackPath);
        if (!File.Exists(assetsPackPath)) File.Create(assetsPackPath).Close();
        
        var assetsSpacePath = EngineContext.Vfs.GetPath(CoreSettings.AssetsSpacePath);
        if (!File.Exists(assetsSpacePath)) File.Create(assetsSpacePath).Close();

        _assetsPointers = IdLookupSerializer.ReadAll(assetsPackPath + ".lookup");
        _entitiesPointers = IdLookupSerializer.ReadAll(assetsSpacePath + ".lookup");
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

    internal T GetAsset<T>(Guid id) where T : Asset
    {
        if (!_loadedAssets.Exists(x => x.Id == id)) return LoadAsset<T>(id);
        return (T)_loadedAssets.Find(x => x.Id == id);
    }

    internal Asset GetUnknownAsset(FileStream stream)
    {
        var typeName = EngineContext.Serializer.ReadString(stream);
        if(string.IsNullOrEmpty(typeName)) return null;
        return EngineContext.AssetsSerializationRegistry.Read(typeName, stream);
    }

    internal void WriteAsset(Asset asset, FileStream stream, BinaryWriter binaryWriter)
    {
        IdLookupSerializer.Write(binaryWriter, asset.Id, (uint)stream.Position);
        var type = asset.GetType();
        
        EngineContext.Serializer.Write($"{type.FullName}, {type.Assembly.GetName().Name}", stream);
        EngineContext.Serializer.BeginObject(stream);
        EngineContext.AssetsSerializationRegistry.Write(type, asset, stream);
        EngineContext.Serializer.EndObject(stream);
    }

    internal void SaveNewAssets()
    {
        var assetsPackPath = EngineContext.Vfs.GetPath(CoreSettings.AssetsPackPath);
        using var stream = File.Open(assetsPackPath, FileMode.Open, FileAccess.ReadWrite);
        
        using var binaryWriter =
            new BinaryWriter(File.Open(assetsPackPath + ".lookup", FileMode.Append, FileAccess.Write));

        EngineContext.Serializer.Open(stream, stream.Length == 0);
        foreach (var asset in _createdAssets)
            WriteAsset(asset, stream, binaryWriter);
        EngineContext.Serializer.Close(stream);
    }

    internal void SaveAsset(Asset asset)
    {
        var assetsPackPath = EngineContext.Vfs.GetPath(CoreSettings.AssetsPackPath);
        using var stream = File.Open(assetsPackPath, FileMode.Open, FileAccess.Write);

        var binaryWriter =
            new BinaryWriter(File.Open(assetsPackPath + ".lookup", FileMode.Append, FileAccess.Write));

        EngineContext.Serializer.Open(stream, stream.Length == 0);
        WriteAsset(asset, stream, binaryWriter);
        EngineContext.Serializer.Close(stream);
    }

    internal T LoadAsset<T>(Guid id) where T : IReferenceable
    {
        using var stream = File.Open(EngineContext.Vfs.GetPath(CoreSettings.AssetsPackPath), FileMode.Open, FileAccess.Read);
        
        stream.Position = _assetsPointers[id];
        var asset = EngineContext.Serializer.Read<T>(stream);
        _loadedAssets.Add(asset as Asset);
        return asset;
    }

    internal Entity LoadEntity(Guid id)
    {
        using var stream = File.Open(EngineContext.Vfs.GetPath(CoreSettings.AssetsSpacePath), FileMode.Open, FileAccess.Read);
        stream.Position = _entitiesPointers[id];
        var entity = EngineContext.Serializer.Read<Entity>(stream);

        EngineContext.DISystem.BuildDependencies(WorldContext.GlobalSpace);
        
        return entity;
    }
}