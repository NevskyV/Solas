using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Solas.Components;
using Solas.ComponentUtils;
using Solas.Interfaces;
using Solas.World;

namespace Solas.Containers;

internal class EntityPool
{
    private int _capacity = 1024;
    private uint _globalInternalIdCounter = 1;

    private ushort[] _versions = new ushort[1024];
    private Guid[] _guids = new Guid[1024];
    private Space[] _spaces = new Space[1024];
    private EntityMetaData[] _metaData = new EntityMetaData[1024];
    private bool[] _isEnableds = new bool[1024];
    private ReactiveProperty<bool>[] _reactiveProperties = new ReactiveProperty<bool>[1024];
    private uint[][] _bitmasks = new uint[1024][];

    private readonly Dictionary<Space, List<Entity>> _entitiesInSpaces = new();
    private readonly Stack<uint> _freeInternalIds = new();

    internal List<IUpdateRunner> UpdateRunners { get; } = [];
    internal List<IUpdateRunner> FixedUpdateRunners { get; } = [];
    internal List<IUpdateRunner> LateUpdateRunners { get; } = [];

    #region Space Registration

    internal void RegisterSpace(Space space)
    {
        _entitiesInSpaces.TryAdd(space, []);
    }

    internal void UnregisterSpace(Space space)
    {
        if (_entitiesInSpaces.TryGetValue(space, out var entities))
        {
            var array = entities.ToArray();
            for (int i = 0; i < array.Length; i++)
            {
                UnregisterEntity(array[i]);
            }

            _entitiesInSpaces.Remove(space);
        }
    }

    #endregion

    #region Entity Registration & LifeCycle

    internal (uint InternalId, ushort Version) RegisterEntity(Guid guid, Space space, EntityMetaData metaData)
    {
        space ??= WorldContext.GlobalSpace;
        RegisterSpace(space);

        uint id;
        ushort version;

        if (_freeInternalIds.Count > 0)
        {
            id = _freeInternalIds.Pop();
            version = _versions[id];
        }
        else
        {
            id = _globalInternalIdCounter++;
            EnsureCapacity(id);
            version = 1;
            _versions[id] = version;
        }

        guid = guid == Guid.Empty ? Guid.NewGuid() : guid;
        metaData = metaData.Equals(default) ? EntityMetaData.CreateDefault() : metaData;

        _guids[id] = guid;
        _spaces[id] = space;
        _metaData[id] = metaData;
        _isEnableds[id] = true;
        _reactiveProperties[id] = null;

        _bitmasks[id] = [];

        return (id, version);
    }

    internal void LinkEntityToSpace(Entity entity)
    {
        if (_entitiesInSpaces.TryGetValue(entity.CurrentSpace, out var list))
        {
            list.Add(entity);
        }
    }

    internal void UnregisterEntity(Entity entity)
    {
        uint id = entity.InternalId;
        if (!IsAlive(entity)) return;

        if (_componentPoolsInSpaces.TryGetValue(entity.CurrentSpace, out var pools))
        {
            foreach (var pool in pools.Values)
            {
                pool.Remove(entity);
            }
        }

        _bitmasks[id] = [];

        if (_spaces[id] != null && _entitiesInSpaces.TryGetValue(_spaces[id], out var list))
        {
            list.Remove(entity);
        }

        _spaces[id] = null;
        _versions[id]++;
        _freeInternalIds.Push(id);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsAlive(Entity entity)
    {
        uint id = entity.InternalId;
        return id < _globalInternalIdCounter && _spaces[id] != null && _versions[id] == entity.Version;
    }

    #endregion

    #region Properties Getters/Setters

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Guid GetGuid(Entity e) => _guids[e.InternalId];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Space GetSpace(Entity e) => _spaces[e.InternalId];

    internal void SetSpace(Entity e, Space newSpace)
    {
        uint id = e.InternalId;
        var oldSpace = _spaces[id];
        if (oldSpace == newSpace) return;

        if (oldSpace != null && _entitiesInSpaces.TryGetValue(oldSpace, out var oldList))
            oldList.Remove(e);

        _spaces[id] = newSpace;
        RegisterSpace(newSpace);
        _entitiesInSpaces[newSpace].Add(e);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal EntityMetaData GetMetaData(Entity e) => _metaData[e.InternalId];

    internal void SetMetaData(Entity e, EntityMetaData meta) => _metaData[e.InternalId] = meta;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReactiveProperty<bool> GetIsEnabled(Entity e)
    {
        uint id = e.InternalId;
        ref var prop = ref _reactiveProperties[id];

        if (prop == null)
        {
            prop = new ReactiveProperty<bool> { Value = _isEnableds[id] };
            prop.OnChange += val => _isEnableds[id] = val;
        }

        return prop;
    }

    internal ReadOnlySpan<IData> GetDataSpan(Entity e)
    {
        if (!_componentPoolsInSpaces.TryGetValue(e.CurrentSpace, out var pools))
            return ReadOnlySpan<IData>.Empty;

        var list = new List<IData>();
        foreach (var pool in pools.Values)
        {
            if (pool.GetComponentFor(e) is IData data)
                list.Add(data);
        }

        return CollectionsMarshal.AsSpan(list);
    }

    internal ReadOnlySpan<Logic> GetLogicSpan(Entity e)
    {
        if (!_componentPoolsInSpaces.TryGetValue(e.CurrentSpace, out var pools))
            return ReadOnlySpan<Logic>.Empty;

        var list = new List<Logic>();
        foreach (var pool in pools.Values)
        {
            if (pool.GetComponentFor(e) is Logic logic)
                list.Add(logic);
        }

        return CollectionsMarshal.AsSpan(list);
    }

    #endregion

    #region Data Methods

    internal void AddData<T>(Entity e, T data) where T : IData
    {
        AddReferences(data, e);
        UpdateBitmask(e, typeof(T));
    }

    internal void RemoveData<T>(Entity e, T data) where T : IData
    {
        RemoveReferences(data, e);
        UpdateBitmask(e, typeof(T));
    }

    internal T GetData<T>(Entity e) where T : IData
    {
        if (_componentPoolsInSpaces.TryGetValue(e.CurrentSpace, out var pools) &&
            pools.TryGetValue(typeof(T), out var pool))
        {
            return ((ComponentPool<T>)pool).Get(e);
        }

        return default;
    }

    #endregion

    #region Logic Methods

    internal void AddLogic<T>(Entity e, T logic) where T : Logic
    {
        AddReferences(logic, e);
        UpdateBitmask(e, typeof(T));
    }

    internal void RemoveLogic<T>(Entity e, T logic) where T : Logic
    {
        RemoveReferences(logic, e);
        UpdateBitmask(e, typeof(T));
    }

    internal T GetLogic<T>(Entity e) where T : Logic
    {
        if (_componentPoolsInSpaces.TryGetValue(e.CurrentSpace, out var pools) &&
            pools.TryGetValue(typeof(T), out var pool))
        {
            return ((ComponentPool<T>)pool).Get(e);
        }

        return null;
    }

    #endregion

    #region Bitmask Management

    private void UpdateBitmask(Entity entity, Type componentType)
    {
        uint id = entity.InternalId;
        var compId = ComponentRegistry.GetId(componentType);
        var chunkIndex = compId / 32;
        var bitIndex = compId % 32;

        ref var mask = ref _bitmasks[id];
        mask ??= [];

        if (chunkIndex >= mask.Length)
        {
            Array.Resize(ref mask, chunkIndex + 1);
        }

        mask[chunkIndex] |= 1u << bitIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal uint[] GetBitmask(uint internalId) => _bitmasks[internalId] ?? [];

    #endregion

    #region Component Pools

    private readonly Dictionary<Space, Dictionary<Type, IComponentPool>> _componentPoolsInSpaces = [];

    private ComponentPool<T> RegisterPool<T>(Space space)
    {
        var type = typeof(T);
        if (!_componentPoolsInSpaces.TryGetValue(space, out var pools))
        {
            pools = new Dictionary<Type, IComponentPool>();
            _componentPoolsInSpaces[space] = pools;
        }

        if (pools.TryGetValue(type, out var componentPool))
            return (ComponentPool<T>)componentPool;

        var pool = new ComponentPool<T>();
        pools[type] = pool;
        return pool;
    }

    private void AddReferences<T>(T component, Entity entity)
    {
        var rawPool = RegisterPool<T>(entity.CurrentSpace);
        rawPool.Add(component, entity);
    }

    private void RemoveReferences<T>(T _, Entity entity)
    {
        var type = typeof(T);
        if (_componentPoolsInSpaces.TryGetValue(entity.CurrentSpace, out var pools) &&
            pools.TryGetValue(type, out var pool))
            pool.Remove(entity);
    }

    #endregion

    #region Search

    internal IEnumerable<Entity> GetEntitiesIn(Space space)
    {
        return _entitiesInSpaces.TryGetValue(space, out var list) ? list : Array.Empty<Entity>();
    }

    internal IEnumerable<Entity> GetEntitiesIn(SpaceFolder spaceFolder)
    {
        if (!_entitiesInSpaces.TryGetValue(spaceFolder.Space, out var entities)) yield break;

        var ids = spaceFolder.EntityIds;
        for (int i = 0; i < entities.Count; i++)
        {
            if (ids.Contains(entities[i].Id))
                yield return entities[i];
        }
    }

    internal IEnumerable<Entity> GetEntitiesInAvailable(Space space)
    {
        var availableSpaces = SpaceTree.GetAllAvailableSpacesFor(space);
        for (int i = 0; i < availableSpaces.Count; i++)
        {
            if (_entitiesInSpaces.TryGetValue(availableSpaces[i], out var list))
            {
                for (int j = 0; j < list.Count; j++)
                    yield return list[j];
            }
        }
    }

    internal IEnumerable<Entity> GetEntitiesWith(Space space, params Type[] types)
    {
        if (types == null || types.Length == 0) yield break;
        if (!_entitiesInSpaces.TryGetValue(space, out var entities)) yield break;

        var totalChunks = ComponentRegistry.Count / 32 + 1;
        var filter = ArrayPool<uint>.Shared.Rent(totalChunks);
        Array.Clear(filter, 0, totalChunks);

        try
        {
            for (int i = 0; i < types.Length; i++)
            {
                var id = ComponentRegistry.GetId(types[i]);
                filter[id / 32] |= 1u << (id % 32);
            }

            for (int i = 0; i < entities.Count; i++)
            {
                var mask = GetBitmask(entities[i].InternalId);
                if (IsMatch(mask, filter, totalChunks))
                    yield return entities[i];
            }
        }
        finally
        {
            ArrayPool<uint>.Shared.Return(filter);
        }
    }

    internal IEnumerable<Entity> GetEntitiesInAvailableWith(Space space, params Type[] types)
    {
        var availableSpaces = SpaceTree.GetAllAvailableSpacesFor(space);
        for (int i = 0; i < availableSpaces.Count; i++)
        {
            foreach (var entity in GetEntitiesWith(availableSpaces[i], types))
            {
                yield return entity;
            }
        }
    }

    private static bool IsMatch(uint[] entityMask, uint[] filter, int filterLength)
    {
        for (var i = 0; i < filterLength; i++)
        {
            var entityChunk = i < entityMask.Length ? entityMask[i] : 0u;
            if ((entityChunk & filter[i]) != filter[i]) return false;
        }

        return true;
    }

    internal IEnumerable<Entity> GetEntitiesByType<T>(Space space)
    {
        var type = typeof(T);
        if (_componentPoolsInSpaces.TryGetValue(space, out var pools) && pools.TryGetValue(type, out var value))
        {
            var pool = (ComponentPool<T>)value;
            return pool.Entities;
        }

        return [];
    }

    internal IEnumerable<Entity> GetEntitiesByTypeInAvailable<T>(Space space)
    {
        var availableSpaces = SpaceTree.GetAllAvailableSpacesFor(space);
        for (int i = 0; i < availableSpaces.Count; i++)
        {
            foreach (var entity in GetEntitiesByType<T>(availableSpaces[i]))
            {
                yield return entity;
            }
        }
    }

    internal IEnumerable<T> GetComponentsByType<T>(Space space)
    {
        var type = typeof(T);
        if (_componentPoolsInSpaces.TryGetValue(space, out var pools) && pools.TryGetValue(type, out var value))
        {
            var pool = (ComponentPool<T>)value;
            return pool.Components;
        }

        return [];
    }

    internal IEnumerable<T> GetComponentsByTypeInAvailable<T>(Space space)
    {
        var availableSpaces = SpaceTree.GetAllAvailableSpacesFor(space);
        for (int i = 0; i < availableSpaces.Count; i++)
        {
            foreach (var component in GetComponentsByType<T>(availableSpaces[i]))
            {
                yield return component;
            }
        }
    }

    internal T GetComponentByType<T>(Space space)
    {
        var type = typeof(T);
        if (_componentPoolsInSpaces.TryGetValue(space, out var pools) && pools.TryGetValue(type, out var value))
        {
            var pool = (ComponentPool<T>)value;
            if (pool.Components.Count > 0)
                return pool.Components[0];
        }

        return default;
    }

    internal T GetComponentByTypeInAvailable<T>(Space space)
    {
        var availableSpaces = SpaceTree.GetAllAvailableSpacesFor(space);
        for (int i = 0; i < availableSpaces.Count; i++)
        {
            var component = GetComponentByType<T>(availableSpaces[i]);
            if (component != null) return component;
        }

        return default;
    }

    internal IEnumerable<IComponentPool> GetComponentPoolsInSpace(Space space)
    {
        if (_componentPoolsInSpaces.TryGetValue(space, out var pools))
            return pools.Values;
        return [];
    }

    #endregion

    #region Runners Registration

    internal void RegisterRunner(IUpdateRunner runner) => UpdateRunners.Add(runner);
    internal void RegisterFixedRunner(IUpdateRunner runner) => FixedUpdateRunners.Add(runner);
    internal void RegisterLateRunner(IUpdateRunner runner) => LateUpdateRunners.Add(runner);

    #endregion

    #region Helpers

    private void EnsureCapacity(uint internalId)
    {
        int index = (int)internalId;
        if (index < _capacity) return;

        int newCapacity = Math.Max(index + 1, _capacity * 2);

        Array.Resize(ref _versions, newCapacity);
        Array.Resize(ref _guids, newCapacity);
        Array.Resize(ref _spaces, newCapacity);
        Array.Resize(ref _metaData, newCapacity);
        Array.Resize(ref _isEnableds, newCapacity);
        Array.Resize(ref _reactiveProperties, newCapacity);
        Array.Resize(ref _bitmasks, newCapacity);

        _capacity = newCapacity;
    }

    #endregion
}