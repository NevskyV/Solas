using Orbitality.Components;
using Orbitality.ComponentUtils;
using Orbitality.Interfaces;
using Orbitality.World;

namespace Orbitality.Containers;

public class EntityPool
{
    private Dictionary<Space, List<Entity>> Entities { get; } = [];
    private Dictionary<Space, Dictionary<Type, IComponentPool>> ComponentPools { get; } = [];

    public List<IUpdateRunner> UpdateRunners { get; } = [];
    public List<IUpdateRunner> FixedUpdateRunners { get; } = [];
    public List<IUpdateRunner> LateUpdateRunners { get; } = [];
    
    #region Registration
    
    public void RegisterNewSpace(Space space)
    {
        Entities.Add(space, new List<Entity>());
        ComponentPools.Add(space, new Dictionary<Type, IComponentPool>());
    }

    private ComponentPool<T> RegisterNewPool<T>(Space space)
    {
        var type = typeof(T);
        if(ComponentPools[space].TryGetValue(type, out var componentPool)) 
            return (ComponentPool<T>)componentPool;
        var pool = new ComponentPool<T>();
        if(!ComponentPools[space].ContainsKey(type))
            ComponentPools[space].Add(type, pool);
        ComponentPools[space][type] = pool;
        return pool;
    }

    public void RegisterEntity(Entity entity)
    {
        Entities[entity.CurrentSpace].Add(entity);
        foreach (var logic in entity.Logics) AddReferences(logic, entity);
        foreach (var data in entity.Data) AddReferences(data, entity);
    }

    public void UnregisterEntity(Entity entity)
    {
        Entities[entity.CurrentSpace].Remove(entity);
        foreach (var logic in entity.Logics) RemoveReferences(logic, entity);
        foreach (var data in entity.Data) RemoveReferences(data, entity);
    }

    public void UnregisterEntityById(Space space, Guid id)
    {
        var entity = Entities[space].FirstOrDefault(e => e.Id == id);
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
        var rawPool = RegisterNewPool<T>(entity.CurrentSpace);
        rawPool.Add(component, entity);
    }

    public void RemoveReferences<T>(T _, Entity entity)
    {
        var type = typeof(T);

        if (ComponentPools[entity.CurrentSpace].TryGetValue(type, out var pool)) pool.Remove(entity);
    }

    #endregion

    #region Search
    
    public Dictionary<Type, IComponentPool> GetTypesWithComponentPoolsIn(Space space)
    {
        return ComponentPools[space];
    }
    
    public List<Entity> GetEntitiesIn(Space space)
    {
        return Entities[space];
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
        foreach (var entity in Entities[space])
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
        if(ComponentPools[space].ContainsKey(type))
        {
            var pool = (ComponentPool<T>)ComponentPools[space][type];
            if (pool.Entities.Count > 0)
                return pool.Entities;
        }
        else if(ComponentPools[Engine.GlobalSpace].ContainsKey(type))
        {
            var pool = (ComponentPool<T>)ComponentPools[Engine.GlobalSpace][type];
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
        if(ComponentPools[space].ContainsKey(type))
        {
            var pool = (ComponentPool<T>)ComponentPools[space][type];
            if (pool.Entities.Count > 0)
                return pool.Components;
        }
        else if(ComponentPools[Engine.GlobalSpace].ContainsKey(type))
        {
            var pool = (ComponentPool<T>)ComponentPools[Engine.GlobalSpace][type];
            if (pool.Entities.Count > 0)
                return pool.Components;
        }
        throw new NullReferenceException("No components found for type " + type);
    }

    #endregion
}