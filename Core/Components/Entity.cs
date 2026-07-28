using Solas.ComponentUtils;
using Solas.Interfaces;
using Solas.World;

namespace Solas.Components;

public readonly record struct Entity : IDisposable, IToggleable, IReferenceable
{
    public uint InternalId { get; }
    public ushort Version { get; }

    public static Entity Null => new(0, 0);
    public bool IsNull => InternalId == 0;
    public bool IsAlive => EngineContext.EntityPool.IsAlive(this);

    private Entity(uint internalId, ushort version)
    {
        InternalId = internalId;
        Version = version;
    }

    public Entity()
    {
        var handle = EngineContext.EntityPool.RegisterEntity(Guid.NewGuid(), WorldContext.GlobalSpace,
            EntityMetaData.CreateDefault());
        InternalId = handle.InternalId;
        Version = handle.Version;
        EngineContext.EntityPool.LinkEntityToSpace(this);
    }

    public Entity(Guid id, Space space = null, EntityMetaData entityMetaData = default)
        : this(EngineContext.EntityPool.RegisterEntity(id, space, entityMetaData))
    {
        EngineContext.EntityPool.LinkEntityToSpace(this);
    }

    private Entity((uint InternalId, ushort Version) handle) : this(handle.InternalId, handle.Version)
    {
    }

    public Guid Id
    {
        get => EngineContext.EntityPool.GetGuid(this);
        init => throw new Exception("You cannot modify this field, use constructor instead.");
    }

    public Space CurrentSpace
    {
        get => EngineContext.EntityPool.GetSpace(this);
        set => EngineContext.EntityPool.SetSpace(this, value);
    }

    public EntityMetaData MetaData
    {
        get => EngineContext.EntityPool.GetMetaData(this);
        set => EngineContext.EntityPool.SetMetaData(this, value);
    }

    public ReactiveProperty<bool> IsEnabled => EngineContext.EntityPool.GetIsEnabled(this);

    public ReadOnlySpan<IData> Data => EngineContext.EntityPool.GetDataSpan(this);
    public ReadOnlySpan<Logic> Logics => EngineContext.EntityPool.GetLogicSpan(this);

    public Guid GetSpaceId() => CurrentSpace?.Id ?? Guid.Empty;

    public void Dispose()
    {
        EngineContext.EntityPool.UnregisterEntity(this);
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

    #region Data Method Group

    public T AddData<T>(T data) where T : IData
    {
        data.Entity = this;
        EngineContext.EntityPool.AddData(this, data);
        return data;
    }

    public void RemoveData<T>(T data) where T : IData
    {
        EngineContext.EntityPool.RemoveData(this, data);
        data.Dispose();
    }

    public T GetData<T>() where T : IData
    {
        return EngineContext.EntityPool.GetData<T>(this);
    }

    #endregion

    #region Logic Method Group

    public T AddLogic<T>() where T : Logic, IInjectable, new()
    {
        var newLogic = new T { Entity = this };
        EngineContext.EntityPool.AddLogic(this, newLogic);
        return newLogic;
    }

    public void RemoveLogic<T>(T logic) where T : Logic, new()
    {
        EngineContext.EntityPool.RemoveLogic(this, logic);
        logic.Dispose();
    }

    public T GetLogic<T>() where T : Logic
    {
        return EngineContext.EntityPool.GetLogic<T>(this);
    }

    #endregion
}