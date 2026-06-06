using System.Runtime.InteropServices;
using Solas.Attributes;
using Solas.ComponentUtils;
using Solas.Interfaces;
using Solas.Serialization;
using Solas.World;

namespace Solas.Components;

public sealed class Entity : IDisposable, IToggleable, IReferenceable
{
    public Guid Id { get; init; }
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
        space ??= WorldContext.GlobalSpace;
        
        //Fill properties
        Id = id;
        MetaData = entityMetaData;
        CurrentSpace = space;

        //Register
        EngineContext.EntityPool.RegisterEntity(this);
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
        var newLogic = new T() { Entity = this };
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

    public void Dispose()
    {
        foreach (var logic in _logics)
        {
            logic.Dispose();
        }
        foreach (var data in _data)
        {
            data.Dispose();
        }
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

    #region Binary

    public Guid GetSpaceId() => CurrentSpace.Id;

    public void Write(BinaryWriter writer)
    {
        writer.Write(Id.ToByteArray());

        writer.Write(MetaData.Name ?? string.Empty);
        writer.Write(MetaData.Tag ?? string.Empty);
        writer.Write(MetaData.Icon);

        // =========================
        // Data
        // =========================

        writer.Write(_data.Count);

        foreach (var data in _data)
        {
            var type = data.GetType();
            writer.Write($"{type.FullName}, {type.Assembly.GetName().Name}");
            DataSerializationRegistry.Write(writer, data, this);
        }

        // =========================
        // Logic
        // =========================

        writer.Write(_logics.Count);

        foreach (var logic in _logics)
        {
            var type = logic.GetType();
            writer.Write($"{type.FullName}, {type.Assembly.GetName().Name}");

            var allInjected = type.GetFields().Where(x =>
                x.CustomAttributes.Any(y => y.ToString() == nameof(AutoInjectAttribute))).ToArray();
            
            writer.Write(allInjected.Count());
            foreach (var info in allInjected)
            {
                Logic obj = (Logic)info.GetValue(logic)!;
                writer.Write(obj.Entity.Id.ToByteArray());
                writer.Write(obj.Entity.CurrentSpace.Id.ToByteArray());
            }
        }
    }

    public IReferenceable Read(BinaryReader reader)
    {
        // =========================
        // Data
        // =========================

        var dataCount = reader.ReadInt32();

        for (var j = 0; j < dataCount; j++)
        {
            var typeName = reader.ReadString();
            var data = DataSerializationRegistry.Read(typeName, reader, out var guids);
            AddData(data);

            if (guids.Length > 0)
                EngineContext.DISystem.AddInjectable(data, guids, CurrentSpace);
        }

        // =========================
        // Logic
        // =========================

        var logicCount = reader.ReadInt32();

        for (var j = 0; j < logicCount; j++)
        {
            var typeName = reader.ReadString();

            var type = Type.GetType(typeName)!;

            var method = GetType().GetMethod(nameof(AddLogic))!.MakeGenericMethod(type);

            var l = (IInjectable)method.Invoke(this, null);
            
            var injectCount = reader.ReadInt32();
            var ids = new (Guid, Guid)[injectCount];
            for (var i = 0; i < injectCount; i++)
            {
                ids[i] = (new Guid(reader.ReadBytes(16)), new Guid(reader.ReadBytes(16)));
            } 
            
            EngineContext.DISystem.AddInjectable(l, ids, CurrentSpace);
        }

        return this;
    }
    
    public static Entity StaticRead(BinaryReader reader, Space space)
    {
        var id = new Guid(reader.ReadBytes(16));

        var metaData = new EntityMetaData(
            reader.ReadString(),
            reader.ReadString(),
            reader.ReadUInt16());

        var entity = new Entity(id, space, metaData);

        entity.Read(reader);
        
        return entity;
    }

    #endregion
    
    public void Destroy() => EngineContext.Destroyer.DestroyEntity(this);
}