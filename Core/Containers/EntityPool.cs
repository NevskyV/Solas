using Core.Components;
using Core.Interfaces;

namespace Core.Containers;

public class EntityPool
{
    public List<Entity> Entities { get; } = [];
    public List<IUpdatable> Updatables { get; } = [];
    public List<IFixedUpdatable> FixedUpdatables { get; } = [];

    public void RegisterEntity(Entity entity)
    {
        Entities.Add(entity);
        foreach (var logic in entity.Logics)
        {
            AddUpdatable(logic);
        }
    }
    
    public void UnregisterEntityById(Guid id)
    {
        var entity = Entities.FirstOrDefault(e => e.Id == id);
        if (entity == null) return;
        Entities.Remove(entity);
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
}