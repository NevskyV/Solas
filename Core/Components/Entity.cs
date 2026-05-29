using System.Runtime.InteropServices;
using Solas.ComponentUtils;
using Solas.Interfaces;
using Solas.World;

namespace Solas.Components;

public class Entity : IDisposable, IToggleable
{
    public Guid Id { get; private set; }
    public EntityMetaData MetaData { get; set; }
    public ReactiveProperty<bool> IsEnabled { get; set; } = new();
    public Space CurrentSpace { get; set; }
    
    private readonly List<IData> _data = [];
    private readonly List<Logic> _logics = [];
    
    public ReadOnlySpan<IData> Data => CollectionsMarshal.AsSpan(_data);
    public ReadOnlySpan<Logic> Logics => CollectionsMarshal.AsSpan(_logics);
    
    public uint[] MaskChunks = [];
    
    public Entity(Guid id = default, Space space = null, EntityMetaData entityMetaData = default)
    {
        //Set default values
        id = id == Guid.Empty ? Guid.NewGuid() : id;
        entityMetaData = entityMetaData == default ? EntityMetaData.CreateDefault() : entityMetaData;
        space ??= Engine.GlobalSpace;
        
        //Fill properties
        Id = id;
        MetaData = entityMetaData;
        CurrentSpace = space;

        //Register
        Engine.Context.EntityPool.RegisterEntity(this);
    }

    #region Data Method Group

    public void AddData<T>(T data) where T : IData
    {
        if (_data.Contains(data)) return;
        _data.Add(data);
        Engine.Context.EntityPool.AddReferences(data, this);
        UpdateMask<T>();
    }

    public void RemoveData<T>(T data) where T : IData
    {
        _data.Remove(data);
        Engine.Context.EntityPool.RemoveReferences(data, this);
        UpdateMask<T>();
    }

    public T GetData<T>() where T : IData
    {
        return (T)_data.First(x => x is T);
    }

    #endregion

    #region Logic Method Group

    public T AddLogic<T>() where T : Logic, new()
    {
        var newLogic = new T() { Entity = this };
        if (_logics.Contains(newLogic)) return newLogic;
        _logics.Add(newLogic);

        Engine.Context.EntityPool.AddReferences(newLogic, this);
        Engine.Context.InjectionSystem.InjectEntity(newLogic, CurrentSpace);
        
        UpdateMask<T>();
        return newLogic;
    }

    public void RemoveLogic<T>(T logic) where T : Logic, new()
    {
        _logics.Remove(logic);
        Engine.Context.EntityPool.RemoveReferences(logic, this);
        UpdateMask<T>();
    }

    public T GetLogic<T>() where T : Logic
    {
        return (T)_logics.First(x => x is T);
    }

    public void Dispose()
    {
        foreach (var logic in _logics) (logic as IDestroyable)?.Destroy();
    }

    #endregion

    private void UpdateMask<T>()
    {
        var id = ComponentRegistry.GetId(typeof(T));
        var chunkIndex = id / 32;
        var bitIndex = id % 32;

        if (chunkIndex >= MaskChunks.Length) Array.Resize(ref MaskChunks, chunkIndex + 1);
        MaskChunks[chunkIndex] |= 1u << bitIndex;
    }

    public async Task SwitchState(bool newState, uint setTime = 0)
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