using System.Runtime.InteropServices;
using Solas.ComponentUtils;
using Solas.Interfaces;
using Solas.World;

namespace Solas.Components;

public sealed class Entity : IDisposable, IToggleable, IReferenceable
{
    private readonly List<IData> _data = [];
    private readonly List<Logic> _logics = [];

    public uint[] MaskChunks = [];


    public Entity(Guid id = default, Space space = null, EntityMetaData entityMetaData = default)
    {
        //Set default values
        id = id == Guid.Empty ? Guid.NewGuid() : id;
        entityMetaData = entityMetaData == default ? EntityMetaData.CreateDefault() : entityMetaData;
        space ??= WorldContext.GlobalSpace;

        //Fill properties
        Id = id;
        MetaData = entityMetaData;
        CurrentSpace = space;

        //Register
        EngineContext.EntityPool.RegisterEntity(this);
    }

    public EntityMetaData MetaData { get; set; }
    public Space CurrentSpace { get; set; }

    public ReadOnlySpan<IData> Data => CollectionsMarshal.AsSpan(_data);
    public ReadOnlySpan<Logic> Logics => CollectionsMarshal.AsSpan(_logics);

    public void Dispose()
    {
        foreach (var logic in _logics) logic.Dispose();
        foreach (var data in _data) data.Dispose();

        EngineContext.EntityPool.UnregisterEntity(this);
    }

    public Guid Id { get; init; }

    public Guid GetSpaceId()
    {
        return CurrentSpace.Id;
    }

    public static IReferenceable SearchReferenceable<T>(Guid id, Guid spaceId) where T : class, IReferenceable
    {
        var space = EngineContext.SpacePool.GetSpace(spaceId);
        if (space != null)
            return EngineContext.EntityPool.GetEntitiesIn(space).First(x => x.Id == id);
        return EngineContext.AssetsPool.LoadEntity(id);
    }

    public ReactiveProperty<bool> IsEnabled { get; set; } = new();

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

    private void UpdateMask<T>()
    {
        var id = ComponentRegistry.GetId(typeof(T));
        var chunkIndex = id / 32;
        var bitIndex = id % 32;

        if (chunkIndex >= MaskChunks.Length) Array.Resize(ref MaskChunks, chunkIndex + 1);
        MaskChunks[chunkIndex] |= 1u << bitIndex;
    }

    #region Data Method Group

    public T AddData<T>(T data) where T : IData
    {
        if (_data.Contains(data)) return default;
        _data.Add(data);
        EngineContext.EntityPool.AddReferences(data, this);
        UpdateMask<T>();
        return data;
    }

    public void RemoveData<T>(T data) where T : IData
    {
        _data.Remove(data);
        EngineContext.EntityPool.RemoveReferences(data, this);
        UpdateMask<T>();
    }

    public T GetData<T>() where T : IData
    {
        return (T)_data.First(x => x is T);
    }

    #endregion

    #region Logic Method Group

    public T AddLogic<T>() where T : Logic, IInjectable, new()
    {
        var newLogic = new T { Entity = this };
        if (_logics.Contains(newLogic)) return newLogic;
        _logics.Add(newLogic);

        EngineContext.EntityPool.AddReferences(newLogic, this);

        UpdateMask<T>();
        return newLogic;
    }

    public void RemoveLogic<T>(T logic) where T : Logic, new()
    {
        _logics.Remove(logic);
        EngineContext.EntityPool.RemoveReferences(logic, this);
        UpdateMask<T>();
    }

    public T GetLogic<T>() where T : Logic
    {
        return (T)_logics.First(x => x is T);
    }

    #endregion
}