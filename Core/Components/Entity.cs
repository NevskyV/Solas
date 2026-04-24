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
    public void AddData<T>(T state) where T : IData
    {
        Data.Add(state);
        UpdateMask<T>();
    } 
    public void RemoveData(IData state) => Data.Remove(state);
    
    public T GetData<T>() where T : IData => (T)Data.First(x => x is T);
    public IEnumerable<T> GetAllData<T>() where T : IData => Data.OfType<T>();

    //Logic Method Group
    public void AddLogic<T>() where T : Logic, new()
    {
        var newLogic = new T();
        newLogic.SetupLogic(this, CurrentSpace.Provider);
        Logics.Add(newLogic);
        CurrentSpace.Initializer.InitializeLogic(newLogic);
        Engine.Context.EntityPool.AddUpdatable(newLogic);

        UpdateMask<T>();
    }
    
    public void RemoveLogic(Logic logic)
    {
        Logics.Remove(logic);
        Engine.Context.EntityPool.RemoveUpdatable(logic);
    }

    public T GetLogic<T>() where T : Logic => (T)Logics.First(x => x is T);
    
    public IEnumerable<T> GetAllLogic<T>() where T : Logic => Logics.OfType<T>();

    public void Dispose()
    {
        foreach (var logic in Logics){
            (logic as IDestroyable)?.Destroy();
        }
        Console.WriteLine($"{MetaData.Name} disposed.");
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