using Core.Components;
using Core.Interfaces;
using Core.World;

namespace Core.Containers;

public class EntityPool
{
    public Dictionary<Space, List<Entity>> Entities { get; } = [];
    public List<IUpdatable> Updatables { get; } = [];
    public List<IFixedUpdatable> FixedUpdatables { get; } = [];

    public void RegisterEntity(Entity entity)
    {
     Entities[entity.CurrentSpace].Add(entity);
        foreach (var logic in entity.Logics)
        {
            AddUpdatable(logic);
        }
    }
    
    public void UnregisterEntityById(Space space, Guid id)
    {
        var entity = Entities[space].FirstOrDefault(e => e.Id == id);
        if (entity != null) UnregisterEntity(entity);
    }
    
    public void UnregisterEntity(Entity entity)
    {
        Entities[entity.CurrentSpace].Remove(entity);
        foreach (var logic in entity.Logics)
        {
            RemoveUpdatable(logic);
        }
    }
    
    public void AddUpdatable(object obj)
    {
        if (obj is IUpdatable u) Updatables.Add(u);
        if (obj is IFixedUpdatable f) FixedUpdatables.Add(f);
    }

    public void RemoveUpdatable(object obj)
    {
        if (obj is IUpdatable u) Updatables.Remove(u);
        if (obj is IFixedUpdatable f) FixedUpdatables.Remove(f);
    }

    public IEnumerable<Entity> GetEntitiesWithData<T>(Space space) where T : IData
    {
        return GetEntitiesBySingleType(space, typeof(T));
    }
    
    public IEnumerable<Entity> GetEntitiesWithLogic<T>(Space space) where T : Logic
    {
        return GetEntitiesBySingleType(space, typeof(T));
    }
    
    public Entity GetEntityWithData<T>(Space space) where T : IData
    {
        return GetEntitiesWithData<T>(space).FirstOrDefault();
    }
    
    public Entity GetEntityWithLogic<T>(Space space) where T : Logic
    {
        return GetEntitiesWithLogic<T>(space).FirstOrDefault();
    }

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

    private IEnumerable<Entity> GetEntitiesBySingleType(Space space, Type type)
    {
        int id = ComponentRegistry.GetId(type);
        int chunkIdx = id / 32;
        uint bitMask = 1u << (id % 32);

        foreach (var entity in Entities[space])
        {
            if (chunkIdx < entity.MaskChunks.Length)
            {
                if ((entity.MaskChunks[chunkIdx] & bitMask) != 0)
                {
                    yield return entity;
                }
            }
        }
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
}