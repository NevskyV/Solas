using Core.Interfaces;
using Core.World;

namespace Core.Components;

public class Entity(Space currentSpace, EntityMetaData metaData) : IDisposable
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public EntityMetaData MetaData { get; set; } = metaData;
    public Space CurrentSpace { get; private set; } = currentSpace;
    
    public List<IData> Data { get; } = new();
    public List<Logic> Logics { get; } = new();

    //Data Method Group
    public void AddData(IData state)
    {
        Data.Add(state);
    }
    
    public T GetData<T>() where T : IData
    {
        return (T)Data.Find(x => x is T);
    }

    public void RemoveData(IData state)
    {
        Data.Remove(state);
    }

    //Logic Method Group
    public void AddLogic<T>() where T : Logic, new()
    {
        var newLogic = new T();
        newLogic.SetupLogic(this, CurrentSpace.Provider);
        Logics.Add(newLogic);
        CurrentSpace.Initializer.InitializeLogic(newLogic);
        Engine.AppContext.Updater.AddUpdatable(newLogic);
    }

    public T GetLogic<T>() where T : Logic
    {
        return (T)Logics.Find(x => x is T);
    }
    
    public void RemoveLogic(Logic logic)
    {
        Logics.Remove(logic);
        Engine.AppContext.Updater.RemoveUpdatable(logic);
    }

    public void Dispose()
    {
        foreach (var logic in Logics){
            (logic as IDestroyable)?.Destroy();
        }
        Console.WriteLine($"{MetaData.Name} disposed.");
    }
}