using Orbitality.Components;
using Orbitality.ComponentUtils;
using Orbitality.Interfaces;
using Orbitality.World;

namespace Orbitality.Containers;

public class EntityPool
{
    private readonly Dictionary<Space, List<Entity>> _entities = [];
    private readonly Dictionary<Space, Dictionary<Type, IComponentPool>> _componentPools = [];
    private readonly List<SpaceFolder> _spaceFolders = [];
    
    public List<IUpdateRunner> UpdateRunners { get; } = [];
    public List<IUpdateRunner> FixedUpdateRunners { get; } = [];
    public List<IUpdateRunner> LateUpdateRunners { get; } = [];
    
    #region Registration
    
    public void RegisterSpace(Space space)
    {
        _entities.Add(space, new List<Entity>());
        _componentPools.Add(space, new Dictionary<Type, IComponentPool>());
    }

    private ComponentPool<T> RegisterPool<T>(Space space)
    {
        var type = typeof(T);
        if(_componentPools[space].TryGetValue(type, out var componentPool)) 
            return (ComponentPool<T>)componentPool;
        var pool = new ComponentPool<T>();
        if(!_componentPools[space].ContainsKey(type))
            _componentPools[space].Add(type, pool);
        _componentPools[space][type] = pool;
        return pool;
    }

    public void RegisterSpaceFolder(SpaceFolder folder)
    {
        _spaceFolders.Add(folder);
    }

    public void RegisterEntity(Entity entity)
    {
        _entities[entity.CurrentSpace].Add(entity);
        foreach (var logic in entity.Logics) AddReferences(logic, entity);
        foreach (var data in entity.Data) AddReferences(data, entity);
    }

    public void UnregisterEntity(Entity entity)
    {
        _entities[entity.CurrentSpace].Remove(entity);
        foreach (var logic in entity.Logics) RemoveReferences(logic, entity);
        foreach (var data in entity.Data) RemoveReferences(data, entity);
    }

    public void UnregisterEntityById(Space space, Guid id)
    {
        var entity = _entities[space].FirstOrDefault(e => e.Id == id);
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

        if (_componentPools[entity.CurrentSpace].TryGetValue(type, out var pool)) pool.Remove(entity);
    }

    #endregion

    #region Search
    
    public SpaceFolder GetSpaceFolderWith(Guid guid)
    {
        return _spaceFolders.Find(x=>x.Guid == guid);
    }
    
    public IEnumerable<SpaceFolder> GetSpaceFoldersWith(List<Guid> guids)
    {
        return _spaceFolders.Where(x=>guids.Contains(x.Guid));
    }
    
    public Dictionary<Type, IComponentPool> GetTypesWithComponentPoolsIn(Space space)
    {
        return _componentPools[space];
    }
    
    public IEnumerable<Entity> GetEntitiesIn(Space space)
    {
        return _entities[space];
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
        foreach (var entity in _entities[space])
            if (IsMatch(entity.MaskChunks, filter))
                result.Add(entity);

        return result;
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
        if(_componentPools[space].ContainsKey(type))
        {
            var pool = (ComponentPool<T>)_componentPools[space][type];
            if (pool.Entities.Count > 0)
                return pool.Entities;
        }
        else if(_componentPools[Engine.GlobalSpace].ContainsKey(type))
        {
            var pool = (ComponentPool<T>)_componentPools[Engine.GlobalSpace][type];
            if (pool.Entities.Count > 0)
                return pool.Entities;
        }
        throw new NullReferenceException("No entities found for type " + type);
    }

    public T GetComponentBySingleType<T>(Space space)
    {
        return GetComponentsBySingleType<T>(space).First();
    }

    public IEnumerable<T> GetComponentsBySingleType<T>(Space space)
    {
        var type = typeof(T);
        if(_componentPools[space].ContainsKey(type))
        {
            var pool = (ComponentPool<T>)_componentPools[space][type];
            if (pool.Entities.Count > 0)
                return pool.Components;
        }
        else if(_componentPools[Engine.GlobalSpace].ContainsKey(type))
        {
            var pool = (ComponentPool<T>)_componentPools[Engine.GlobalSpace][type];
            if (pool.Entities.Count > 0)
                return pool.Components;
        }
        throw new NullReferenceException("No components found for type " + type);
    }

    public IEnumerable<IComponentPool> GetComponentPoolsByType(Type type)
    {
        return _componentPools.Values.Select(x => x[type]);
    }

    #endregion
}