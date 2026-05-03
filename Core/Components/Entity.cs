using Newtonsoft.Json;
using Orbitality.ComponentUtils;
using Orbitality.Interfaces;
using Orbitality.World;

namespace Orbitality.Components;

[Serializable]
public class Entity(Space currentSpace, EntityMetaData metaData) : IDisposable
{
    public EntityMetaData MetaData { get; set; } = metaData;
    public ReactiveProperty<bool> IsEnabled { get; private set; } = new(true);
    [JsonIgnore] public Guid Id { get; private set; } = Guid.NewGuid();
    [JsonIgnore] public Space CurrentSpace { get; set; } = currentSpace;

    public HashSet<EntityModifier> Modifiers { get; init; } = new();
    public HashSet<IData> Data { get; } = new();
    public HashSet<Logic> Logics { get; } = new();
    [JsonIgnore] public uint[] MaskChunks = Array.Empty<uint>();

    #region Data Method Group

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

    public T GetData<T>() where T : IData
    {
        return (T)Data.First(x => x is T);
    }

    #endregion

    #region Logic Method Group

    public T AddLogic<T>() where T : Logic, new()
    {
        var newLogic = new T();
        newLogic.SetupLogic(this, CurrentSpace.Provider);
        if (!Logics.Add(newLogic)) return newLogic;

        Engine.Context.EntityPool.AddReferences(newLogic, this);
        UpdateMask<T>();
        return newLogic;
    }

    public void RemoveLogic<T>(T logic) where T : Logic, new()
    {
        Logics.Remove(logic);
        Engine.Context.EntityPool.RemoveReferences(logic, this);
        UpdateMask<T>();
    }

    public T GetLogic<T>() where T : Logic
    {
        return (T)Logics.First(x => x is T);
    }

    public void Dispose()
    {
        foreach (var logic in Logics) (logic as IDestroyable)?.Destroy();
        foreach (var modifier in Modifiers) modifier.IsEnabled = false;
    }

    #endregion

    #region Modificators Method Group

    public void AddModifier<T>() where T : EntityModifier
    {
        var newModifier = (T)Activator.CreateInstance(typeof(T), this);
        Modifiers.Add(newModifier);
    }
    
    public void SetModifierState<T>(bool state) where T : EntityModifier
    {
        Modifiers.First(x => x is T).IsEnabled = state;
    }

    #endregion

    public void UpdateMask<T>()
    {
        var id = ComponentRegistry.GetId(typeof(T));
        var chunkIndex = id / 32;
        var bitIndex = id % 32;

        if (chunkIndex >= MaskChunks.Length) Array.Resize(ref MaskChunks, chunkIndex + 1);

        MaskChunks[chunkIndex] |= 1u << bitIndex;
    }

    public async void SwitchState(bool newState, uint setTime = 0)
    {
        var oldValue = IsEnabled.Value;
        IsEnabled.Value = newState;
        if (setTime > 0)
        {
            await Task.Delay((int)setTime);
            IsEnabled.Value = oldValue;
        }
    }
}