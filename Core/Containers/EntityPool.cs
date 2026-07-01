using Solas.Components;
using Solas.ComponentUtils;
using Solas.Interfaces;
using Solas.World;

namespace Solas.Containers;

internal class EntityPool
{
    private readonly Dictionary<Space, Dictionary<Type, IComponentPool>> _componentPoolsInSpaces = [];
    private readonly Dictionary<Space, List<Entity>> _entitiesInSpaces = [];

    internal List<IUpdateRunner> UpdateRunners { get; } = [];
    internal List<IUpdateRunner> FixedUpdateRunners { get; } = [];
    internal List<IUpdateRunner> LateUpdateRunners { get; } = [];

    #region Registration

    internal void RegisterSpace(Space space)
    {
        _entitiesInSpaces.Add(space, new List<Entity>());
        _componentPoolsInSpaces.Add(space, new Dictionary<Type, IComponentPool>());
    }

    internal void UnregisterSpace(Space space)
    {
        _entitiesInSpaces.Remove(space);
        _componentPoolsInSpaces.Remove(space);
    }

    private ComponentPool<T> RegisterPool<T>(Space space)
    {
        var type = typeof(T);
        if (_componentPoolsInSpaces[space].TryGetValue(type, out var componentPool))
            return (ComponentPool<T>)componentPool;
        var pool = new ComponentPool<T>();
        if (!_componentPoolsInSpaces[space].ContainsKey(type))
            _componentPoolsInSpaces[space].Add(type, pool);
        _componentPoolsInSpaces[space][type] = pool;
        return pool;
    }

    internal void RegisterEntity(Entity entity)
    {
        entity.CurrentSpace ??= _entitiesInSpaces.Keys.Last();
        _entitiesInSpaces[entity.CurrentSpace].Add(entity);
        foreach (var logic in entity.Logics) AddReferences(logic, entity);
        foreach (var data in entity.Data) AddReferences(data, entity);
    }

    internal void UnregisterEntity(Entity entity)
    {
        _entitiesInSpaces[entity.CurrentSpace].Remove(entity);
        var folder = Query.GetAllSpaceFoldersIn(entity.CurrentSpace).FirstOrDefault(f => f.EntityIds.Contains(entity.Id));
        folder?.EntityIds.Remove(entity.Id);
        
        foreach (var logic in entity.Logics) RemoveReferences(logic, entity);
        foreach (var data in entity.Data) RemoveReferences(data, entity);
    }

    internal void UnregisterEntityById(Space space, Guid id)
    {
        var entity = _entitiesInSpaces[space].FirstOrDefault(e => e.Id == id);
        if (entity != null) UnregisterEntity(entity);
    }

    internal void RegisterRunner(IUpdateRunner runner)
    {
        UpdateRunners.Add(runner);
    }

    internal void RegisterFixedRunner(IUpdateRunner runner)
    {
        FixedUpdateRunners.Add(runner);
    }

    internal void RegisterLateRunner(IUpdateRunner runner)
    {
        LateUpdateRunners.Add(runner);
    }

    internal void AddReferences<T>(T component, Entity entity)
    {
        var rawPool = RegisterPool<T>(entity.CurrentSpace);
        rawPool.Add(component, entity);
    }

    internal void RemoveReferences<T>(T _, Entity entity)
    {
        var type = typeof(T);

        if (_componentPoolsInSpaces[entity.CurrentSpace].TryGetValue(type, out var pool)) pool.Remove(entity);
    }

    #endregion

    #region Search

    internal IEnumerable<Entity> GetEntitiesIn(Space space)
    {
        return _entitiesInSpaces[space];
    }
    
    internal IEnumerable<Entity> GetEntitiesIn(SpaceFolder spaceFolder)
    {
        return _entitiesInSpaces[spaceFolder.Space].Where(e => spaceFolder.EntityIds.Contains(e.Id));
    }

    internal IEnumerable<Entity> GetEntitiesInAvailable(Space space)
    {
        return _entitiesInSpaces
            .Where(x => SpaceTree.GetAllAvailableSpacesFor(space).Contains(x.Key))
            .SelectMany(x => x.Value);
    }

    internal IEnumerable<Entity> GetEntitiesWith(Space space, params Type[] types)
    {
        if (types == null || types.Length == 0) throw new NullReferenceException("No entities found with " + types);

        var totalChunks = ComponentRegistry.Count / 32 + 1;
        Span<uint> filter = stackalloc uint[totalChunks];
        filter.Clear();

        foreach (var type in types)
        {
            var id = ComponentRegistry.GetId(type);
            filter[id / 32] |= 1u << (id % 32);
        }

        var result = new List<Entity>();
        foreach (var entity in _entitiesInSpaces[space])
            if (IsMatch(entity.MaskChunks, filter))
                result.Add(entity);

        return result;
    }

    internal IEnumerable<Entity> GetEntitiesInAvailableWith(Space space, params Type[] types)
    {
        return SpaceTree.GetAllAvailableSpacesFor(space).SelectMany(x => GetEntitiesWith(x, types));
    }

    private bool IsMatch(uint[] entityMask, ReadOnlySpan<uint> filter)
    {
        for (var i = 0; i < filter.Length; i++)
        {
            var entityChunk = i < entityMask.Length ? entityMask[i] : 0;
            if ((entityChunk & filter[i]) != filter[i]) return false;
        }

        return true;
    }

    internal IEnumerable<Entity> GetEntitiesByType<T>(Space space)
    {
        var type = typeof(T);
        if (_componentPoolsInSpaces[space].ContainsKey(type))
        {
            var pool = (ComponentPool<T>)_componentPoolsInSpaces[space][type];
            if (pool.Entities.Count > 0)
                return pool.Entities;
        }

        return [];
    }

    internal IEnumerable<Entity> GetEntitiesByTypeInAvailable<T>(Space space)
    {
        return SpaceTree.GetAllAvailableSpacesFor(space).SelectMany(GetEntitiesByType<T>);
    }

    internal IEnumerable<T> GetComponentsByType<T>(Space space)
    {
        var type = typeof(T);
        if (_componentPoolsInSpaces[space].TryGetValue(type, out var value))
        {
            var pool = (ComponentPool<T>)value;
            if (pool.Entities.Count > 0)
                return pool.Components;
        }

        return [];
    }

    internal IEnumerable<T> GetComponentsByTypeInAvailable<T>(Space space)
    {
        return SpaceTree.GetAllAvailableSpacesFor(space).SelectMany(GetComponentsByType<T>);
    }

    internal T GetComponentByType<T>(Space space)
    {
        return GetComponentsByType<T>(space).FirstOrDefault();
    }

    internal T GetComponentByTypeInAvailable<T>(Space space)
    {
        return GetComponentsByTypeInAvailable<T>(space).FirstOrDefault();
    }

    internal Entity TryGetEntityFor(object component, Space hintSpace = null)
    {
        if (component == null)
            return null;

        if (hintSpace != null)
        {
            var found = FindEntityForInSpace(component, hintSpace);
            if (found != null)
                return found;
        }

        foreach (var (space, _) in _componentPoolsInSpaces)
        {
            if (hintSpace != null && space == hintSpace)
                continue;

            var found = FindEntityForInSpace(component, space);
            if (found != null)
                return found;
        }

        return null;
    }

    private Entity FindEntityForInSpace(object component, Space space)
    {
        if (!_componentPoolsInSpaces.TryGetValue(space, out var pools))
            return null;

        foreach (var pool in pools.Values)
        {
            var entity = pool.FindEntityFor(component);
            if (entity != null)
                return entity;
        }

        return null;
    }

    internal IEnumerable<IComponentPool> GetComponentPoolsInSpace(Space space)
    {
        return _componentPoolsInSpaces[space].Values;
    }

    #endregion
}