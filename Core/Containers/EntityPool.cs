using Core.Components;
using Core.Interfaces;
using Core.World;

namespace Core.Containers;

public class EntityPool
{
    public Dictionary<Space, List<Entity>> Entities { get; } = [];
    public Dictionary<Type, IComponentPool> ComponentPools { get; } = [];

    public List<IUpdateRunner> UpdateRunners { get; } = [];
    public List<IUpdateRunner> FixedUpdateRunners { get; } = [];
    public List<IUpdateRunner> LateUpdateRunners { get; } = [];

    #region Registration

    public void RegisterEntity(Entity entity)
    {
        Entities[entity.CurrentSpace].Add(entity);
        foreach (var logic in entity.Logics)
        {
            AddReferences(logic, entity);
        }

        foreach (var data in entity.Data)
        {
            AddReferences(data, entity);
        }
    }
    
    public void UnregisterEntity(Entity entity)
    {
        Entities[entity.CurrentSpace].Remove(entity);
        foreach (var logic in entity.Logics)
        {
            RemoveReferences(logic, entity);
        }
        foreach (var data in entity.Data)
        {
            RemoveReferences(data, entity);
        }
    }
    
    public void UnregisterEntityById(Space space, Guid id)
    {
        var entity = Entities[space].FirstOrDefault(e => e.Id == id);
        if (entity != null) UnregisterEntity(entity);
    }
    
    private void RegisterPipelines<T>(ComponentPool<T> pool)
    {
        var type = typeof(T);

        if (typeof(IUpdatable).IsAssignableFrom(type))
        {
            var runnerType = typeof(UpdateRunner<>).MakeGenericType(type);
            UpdateRunners.Add((IUpdateRunner)Activator.CreateInstance(runnerType, pool));
        }

        if (typeof(IFixedUpdatable).IsAssignableFrom(type))
        {
            var runnerType = typeof(FixedUpdateRunner<>).MakeGenericType(type);
            FixedUpdateRunners.Add((IUpdateRunner)Activator.CreateInstance(runnerType, pool));
        }

        if (typeof(ILateUpdatable).IsAssignableFrom(type))
        {
            var runnerType = typeof(LateUpdateRunner<>).MakeGenericType(type);
            LateUpdateRunners.Add((IUpdateRunner)Activator.CreateInstance(runnerType, pool));
        }
    }
    
    public void AddReferences<T>(T component, Entity entity)
    {
        var type = typeof(T);

        if (!ComponentPools.TryGetValue(type, out var rawPool))
        {
            var pool = new ComponentPool<T>();
            ComponentPools[type] = pool;

            RegisterPipelines(pool);

            rawPool = pool;
        }

        ((ComponentPool<T>)rawPool).Add(component!, entity);
    }

    public void RemoveReferences<T>(T _, Entity entity)
    {
        var type = typeof(T);

        if (ComponentPools.TryGetValue(type, out var pool))
        {
            pool.Remove(entity);
        }
    }

    #endregion

    #region Search
    

    public Entity GetEntityWith(Space space, params Type[] types)
    {
        return GetEntitiesWith(space, types).FirstOrDefault();
    }
    
    public IEnumerable<Entity> GetEntitiesWith(Space space, params Type[] types)
    {
        if (types == null || types.Length == 0) return Enumerable.Empty<Entity>();
        
        int totalChunks = (ComponentRegistry.Count / 32) + 1;
        Span<uint> filter = stackalloc uint[totalChunks];
        filter.Clear();

        foreach (var type in types)
        {
            int id = ComponentRegistry.GetId(type);
            filter[id / 32] |= (1u << (id % 32));
        }
        
        var result = new List<Entity>();
        foreach (var entity in Entities[space])
        {
            if (IsMatch(entity.MaskChunks, filter))
            {
                result.Add(entity);
            }
        }
        return result;
    }

    private bool IsMatch(uint[] entityMask, ReadOnlySpan<uint> filter)
    {
        for (int i = 0; i < filter.Length; i++)
        {
            uint entityChunk = i < entityMask.Length ? entityMask[i] : 0;
            if ((entityChunk & filter[i]) != filter[i])
            {
                return false;
            }
        }
        return true;
    }
    
    public IEnumerable<Entity> GetEntitiesBySingleType<T>(Space space)
    {
        return ((ComponentPool<T>)ComponentPools[typeof(T)]).Entities.Where(e => 
            e.CurrentSpace == space || e.CurrentSpace == Engine.WorldContext.GlobalSpace);
    }
    
    public IEnumerable<T> GetComponentsBySingleType<T>(Space space)
    {
        var result = new List<T>();
        var pool = (ComponentPool<T>)ComponentPools[typeof(T)];
        for (int i = 0; i < pool.Entities.Count; i++)
        {
            var e = pool.Entities[i];
            if (e.CurrentSpace == space || e.CurrentSpace == Engine.WorldContext.GlobalSpace)
            {
                result.Add(pool.Components[i]);
            }
        }
        return result;
    }

    #endregion
}