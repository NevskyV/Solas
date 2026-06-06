using Solas.Assets;
using Solas.Components;
using Solas.Interfaces;
using Solas.Serialization;

namespace Solas.Containers;

public class AssetsPool
{
    private readonly List<Asset> _createdAssets = [];
    private readonly List<Asset> _loadedAssets = [];
    private Dictionary<Guid, uint> _assetsPointers;
    private Dictionary<Guid, uint> _entitiesPointers;

    public void ReadPointers()
    {
        _assetsPointers = SearchIdSerializer.ReadAll(Engine.CoreSettings.AssetsPackPath + ".lookup");
        _entitiesPointers = SearchIdSerializer.ReadAll(Engine.CoreSettings.AssetsSpacePath + ".lookup");
    }
    
    public uint GetAssetPointer(Guid id) => _assetsPointers[id];
    public uint GetEntitiesPointer(Guid id) => _entitiesPointers[id];

    public void RegisterNewAsset(Asset asset)
    {
        _createdAssets.Add(asset);
    }
    
    public Asset GetLoadedAsset(Guid id)
    {
        var res = _loadedAssets.Find(x => x.Id == id);
        return res;
    }
    
    public T GetAsset<T>(Guid id) where T : Asset, new()
    {
        if (!_loadedAssets.Exists(x => x.Id == id))
        {
            return LoadAsset<T>(id);
        }
        return (T)_loadedAssets.Find(x => x.Id == id);
    }

    public void SaveAllNewAssets()
    {
        using var stream = File.Open(Engine.CoreSettings.AssetsPackPath, FileMode.Append, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        foreach (var asset in _createdAssets)
        {
            SearchIdSerializer.Write(Engine.CoreSettings.AssetsPackPath + ".lookup", asset.Id, (uint)stream.Position);
        
            writer.Write(asset.Id.ToByteArray());
            asset.Write(writer);
        }
    }
    
    public static void SaveAsset(Asset asset)
    {
        using var stream = File.Open(Engine.CoreSettings.AssetsPackPath, FileMode.Append, FileAccess.Write);
        using var writer = new BinaryWriter(stream);
        
        SearchIdSerializer.Write(Engine.CoreSettings.AssetsPackPath + ".lookup", asset.Id, (uint)stream.Position);
        
        writer.Write(asset.Id.ToByteArray());
        asset.Write(writer);
    }

    public T LoadAsset<T>(Guid id) where T : IReferenceable, new()
    {
        using var stream = File.Open(Engine.CoreSettings.AssetsPackPath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream);
        
        stream.Position = _assetsPointers[id];
        var asset = (T)new T().Read(reader);
        _loadedAssets.Add(asset as Asset);
        return asset;
    }

    public Entity LoadEntity(Guid id)
    {
        using var stream = File.Open(Engine.CoreSettings.AssetsSpacePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream);
        
        stream.Position = _assetsPointers[id];
        var entity = Entity.StaticRead(reader, Engine.GlobalSpace);
        Engine.Context.DISystem.BuildDependencies(Engine.GlobalSpace);
        return entity;
    }

    // public void __Inject((Guid id, Guid spaceId)[] ids)
    // {
    //     var sys = Engine.Context.DISystem;
    //     fff = sys.Inject<T>();
    //     ggg = sys.AutoInject<T>();
    // }
}