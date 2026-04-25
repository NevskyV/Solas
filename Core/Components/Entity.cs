using Core.Interfaces;
using Core.World;
using Newtonsoft.Json;

namespace Core.Components;

[Serializable]
public class Entity(Space currentSpace, EntityMetaData metaData) : IDisposable
{
    [JsonIgnore] public Guid Id { get; private set; } = Guid.NewGuid();
    public EntityMetaData MetaData { get; set; } = metaData;
    [JsonIgnore] public Space CurrentSpace { get; set; } = currentSpace;

    public HashSet<IData> Data { get; init; } = new();
    public HashSet<Logic> Logics { get; init; } = new();
    [JsonIgnore] public uint[] MaskChunks = Array.Empty<uint>();

    //Data Method Group
    public void AddData<T>(T data) where T : IData
    {
        if (!Data.Add(data)) return;
        Engine.Context.EntityPool.AddReferences(data, this);
        UpdateMask<T>();
    }

    public void RemoveData<T>(T data) where T : IData
    {
        Data.Remove(data);
        Engine.Context.EntityPool.RemoveReferences(data, this);
        UpdateMask<T>();
    }

    public T GetData<T>() where T : IData => (T)Data.First(x => x is T);
    
    //Logic Method Group
    public void AddLogic<T>() where T : Logic, new()
    {
        var newLogic = new T();
        newLogic.SetupLogic(this, CurrentSpace.Provider);
        if (!Logics.Add(newLogic)) return;
        
        Engine.Context.EntityPool.AddReferences(newLogic, this);
        UpdateMask<T>();
    }
    
    public void RemoveLogic<T>(T logic) where T : Logic, new()
    {
        Logics.Remove(logic);
        Engine.Context.EntityPool.RemoveReferences(logic, this);
        UpdateMask<T>();
    }

    public T GetLogic<T>() where T : Logic => (T)Logics.First(x => x is T);

    public void Dispose()
    {
        foreach (var logic in Logics){
            (logic as IDestroyable)?.Destroy();
        }
    }
    
    public void UpdateMask<T>()
    {
        int id = ComponentRegistry.GetId(typeof(T));
        int chunkIndex = id / 32;
        int bitIndex = id % 32;

        if (chunkIndex >= MaskChunks.Length)
        {
            Array.Resize(ref MaskChunks, chunkIndex + 1);
        }

        MaskChunks[chunkIndex] |= (1u << bitIndex);
    }
}