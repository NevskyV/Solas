using Solas.Components;
using Solas.ComponentUtils;
using Solas.Interfaces;
using Solas.World;

namespace Solas.Containers;

public class EntityPool
{
    private readonly Dictionary<Space, List<Entity>> _entitiesInSpaces = [];
    private readonly Dictionary<Space, Dictionary<Type, IComponentPool>> _componentPoolsInSpaces = [];
    
    public List<IUpdateRunner> UpdateRunners { get; } = [];
    public List<IUpdateRunner> FixedUpdateRunners { get; } = [];
    public List<IUpdateRunner> LateUpdateRunners { get; } = [];
    
    #region Registration
    
    public void RegisterSpace(Space space)
    {
        _entitiesInSpaces.Add(space, new List<Entity>());
        _componentPoolsInSpaces.Add(space, new Dictionary<Type, IComponentPool>());
    }
    
    public void UnregisterSpace(Space space)
    {
        _entitiesInSpaces.Remove(space);
        _componentPoolsInSpaces.Remove(space);
    }

    private ComponentPool<T> RegisterPool<T>(Space space)
    {
        var type = typeof(T);
        if(_componentPoolsInSpaces[space].TryGetValue(type, out var componentPool)) 
            return (ComponentPool<T>)componentPool;
        var pool = new ComponentPool<T>();
        if(!_componentPoolsInSpaces[space].ContainsKey(type))
            _componentPoolsInSpaces[space].Add(type, pool);
        _componentPoolsInSpaces[space][type] = pool;
        return pool;
    }

    public void RegisterEntity(Entity entity)
    {
        _entitiesInSpaces[entity.CurrentSpace].Add(entity);
        foreach (var logic in entity.Logics) AddReferences(logic, entity);
        foreach (var data in entity.Data) AddReferences(data, entity);
    }

    public void UnregisterEntity(Entity entity)
    {
        _entitiesInSpaces[entity.CurrentSpace].Remove(entity);
        foreach (var logic in entity.Logics) RemoveReferences(logic, entity);
        foreach (var data in entity.Data) RemoveReferences(data, entity);
    }

    public void UnregisterEntityById(Space space, Guid id)
    {
        var entity = _entitiesInSpaces[space].FirstOrDefault(e => e.Id == id);
        if (entity != null) UnregisterEntity(entity);
    }

    public void RegisterRunner(IUpdateRunner runner)
    {
        UpdateRunners.Add(runner);
    }
    
    public void RegisterFixedRunner(IUpdateRunner runner)
    {
        FixedUpdateRunners.Add(runner);
    }
    
    public void RegisterLateRunner(IUpdateRunner runner)
    {
        LateUpdateRunners.Add(runner);
    }

    public void AddReferences<T>(T component, Entity entity)
    {
        var rawPool = RegisterPool<T>(entity.CurrentSpace);
        rawPool.Add(component, entity);
    }

    public void RemoveReferences<T>(T _, Entity entity)
    {
        var type = typeof(T);

        if (_componentPoolsInSpaces[entity.CurrentSpace].TryGetValue(type, out var pool)) pool.Remove(entity);
    }

    #endregion

    #region Search
    
    public Dictionary<Type, IComponentPool> GetTypesWithComponentPoolsIn(Space space)
    {
        return _componentPoolsInSpaces[space];
    }
    
    public IEnumerable<Entity> GetEntitiesIn(Space space)
    {
        return _entitiesInSpaces[space];
    }
    
    public IEnumerable<Entity> GetEntitiesInAllAvailable(Space space)
    {
        return _entitiesInSpaces
            .Where(x => SpaceTree.GetAllAvailableSpacesFor(space).Contains(x.Key))
            .SelectMany(x => x.Value);
    }

    public IEnumerable<Entity> GetEntitiesWith(Space space, params Type[] types)
    {
        if (types == null || types.Length == 0) throw new NullReferenceException("No entities found with "  + types);

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
    
    public IEnumerable<Entity> GetEntitiesInAllAvailableWith(Space space, params Type[] types)
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

    public IEnumerable<Entity> GetEntitiesBySingleType<T>(Space space)
    {
        var type = typeof(T);
        if(_componentPoolsInSpaces[space].ContainsKey(type))
        {
            var pool = (ComponentPool<T>)_componentPoolsInSpaces[space][type];
            if (pool.Entities.Count > 0)
                return pool.Entities;
        }

        return [];
    }
    
    public IEnumerable<Entity> GetEntitiesBySingleTypeInAllAvailable<T>(Space space)
    {
        return SpaceTree.GetAllAvailableSpacesFor(space).SelectMany(GetEntitiesBySingleType<T>);
    }

    public IEnumerable<T> GetComponentsBySingleType<T>(Space space)
    {
        var type = typeof(T);
        if(_componentPoolsInSpaces[space].ContainsKey(type))
        {
            var pool = (ComponentPool<T>)_componentPoolsInSpaces[space][type];
            if (pool.Entities.Count > 0)
                return pool.Components;
        }
        return [];
    }
    
    public IEnumerable<T> GetComponentsBySingleTypeInAllAvailable<T>(Space space)
    {
        return SpaceTree.GetAllAvailableSpacesFor(space).SelectMany(GetComponentsBySingleType<T>);
    }
    
    public T GetComponentBySingleType<T>(Space space)
    {
        return GetComponentsBySingleType<T>(space).First();
    }
    
    public T GetComponentBySingleTypeInAllAvailable<T>(Space space)
    {
        return GetComponentsBySingleTypeInAllAvailable<T>(space).First();
    }

    public Entity TryGetEntityFor(object component, Space? hintSpace = null)
    {
        if (component == null)
            return null;

        if (hintSpace != null)
        {
            Entity found = FindEntityForInSpace(component, hintSpace);
            if (found != null)
                return found;
        }

        foreach (var (space, _) in _componentPoolsInSpaces)
        {
            if (hintSpace != null && space == hintSpace)
                continue;

            Entity found = FindEntityForInSpace(component, space);
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
            Entity entity = pool.FindEntityFor(component);
            if (entity != null)
                return entity;
        }

        return null;
    }

    public IEnumerable<IComponentPool> GetComponentPoolsInSpace(Space space)
    {
        return _componentPoolsInSpaces[space].Values;
    }

    #endregion
}