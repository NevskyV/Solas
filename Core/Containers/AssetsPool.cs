using Solas.Assets;
using Solas.Components;
using Solas.Interfaces;
using Solas.Serialization.Binary;

namespace Solas.Containers;

internal class AssetsPool
{
    private readonly List<Asset> _createdAssets = [];
    private readonly List<Asset> _loadedAssets = [];
    private Dictionary<Guid, uint> _assetsPointers;
    private Dictionary<Guid, uint> _prefabPointers;

    internal void ReadPointers()
    {
        var assetsPackPath = EngineContext.Vfs.GetPath(WorldContext.CoreSettings.AssetsPackPath);
        if (!File.Exists(assetsPackPath) && assetsPackPath != null) File.Create(assetsPackPath).Close();
        
        var assetsSpacePath = EngineContext.Vfs.GetPath(WorldContext.CoreSettings.AssetsSpacePath);
        if (!File.Exists(assetsSpacePath) && assetsSpacePath != null) File.Create(assetsSpacePath).Close();

        _assetsPointers = IdLookupSerializer.ReadAll(assetsPackPath + ".lookup");
        _prefabPointers = IdLookupSerializer.ReadAll(assetsSpacePath + ".lookup");
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
    
    internal void WritePrefab(Entity entity, FileStream stream, BinaryWriter binaryWriter)
    {
        IdLookupSerializer.Write(binaryWriter, entity.Id, (uint)stream.Position);
        
        EngineContext.Serializer.BeginObject(stream);
        EngineContext.Serializer.Write(entity, stream);
        EngineContext.Serializer.EndObject(stream);
    }

    internal void SaveNewAssets()
    {
        if(_createdAssets.Count == 0) return;
        
        var assetsPackPath = EngineContext.Vfs.GetPath(WorldContext.CoreSettings.AssetsPackPath);
        using var stream = File.Open(assetsPackPath, FileMode.Open, FileAccess.ReadWrite);
        
        using var binaryWriter =
            new BinaryWriter(File.Open(assetsPackPath + ".lookup", FileMode.Append, FileAccess.Write));

        EngineContext.Serializer.Open(stream, stream.Length == 0);
        foreach (var asset in _createdAssets)
            WriteAsset(asset, stream, binaryWriter);
        EngineContext.Serializer.Close(stream);
        
        binaryWriter.Flush();
        binaryWriter.Close();
    }

    internal void SaveAsset(Asset asset)
    {
        var assetsPackPath = EngineContext.Vfs.GetPath(WorldContext.CoreSettings.AssetsPackPath);
        using var stream = File.Open(assetsPackPath, FileMode.Open, FileAccess.ReadWrite);

        var binaryWriter =
            new BinaryWriter(File.Open(assetsPackPath + ".lookup", FileMode.Append, FileAccess.Write));

        EngineContext.Serializer.Open(stream, stream.Length == 0);
        WriteAsset(asset, stream, binaryWriter);
        EngineContext.Serializer.Close(stream);
        
        binaryWriter.Flush();
        binaryWriter.Close();
    }

    internal T LoadAsset<T>(Guid id) where T : IReferenceable
    {
        using var stream = File.Open(EngineContext.Vfs.GetPath(WorldContext.CoreSettings.AssetsPackPath), FileMode.Open, FileAccess.Read);
        
        stream.Position = _assetsPointers[id];
        var asset = EngineContext.Serializer.Read<T>(stream);
        _loadedAssets.Add(asset as Asset);
        return asset;
    }
    
    internal void SaveAsPrefab(Entity entity)
    {
        var assetsSpacePath = EngineContext.Vfs.GetPath(WorldContext.CoreSettings.AssetsSpacePath);
        using var stream = File.Open(assetsSpacePath, FileMode.Open, FileAccess.ReadWrite);

        var binaryWriter =
            new BinaryWriter(File.Open(assetsSpacePath + ".lookup", FileMode.Append, FileAccess.Write));

        EngineContext.Serializer.Open(stream, stream.Length == 0);
        WritePrefab(entity, stream, binaryWriter);
        EngineContext.Serializer.Close(stream);
        
        binaryWriter.Flush();
        binaryWriter.Close();
    }

    internal Entity LoadPrefab(Guid id)
    {
        using var stream = File.Open(EngineContext.Vfs.GetPath(WorldContext.CoreSettings.AssetsSpacePath), FileMode.Open, FileAccess.Read);
        stream.Position = _prefabPointers[id];
        var entity = EngineContext.Serializer.Read<Entity>(stream);

        EngineContext.DISystem.BuildDependencies(WorldContext.GlobalSpace);
        
        return entity;
    }
}