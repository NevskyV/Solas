using Core.Components;

namespace Core.Containers;

public class EntityPool
{
    private List<Entity> Entities { get; } = [];

    public void RegisterEntity(Entity entity)
    {
        Entities.Add(entity);
    }
    
    public void UnregisterEntityById(Guid id)
    {
        Entities.RemoveAll(entity => entity.Id == id);
    }
}