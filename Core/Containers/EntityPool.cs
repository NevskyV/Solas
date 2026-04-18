using Core.Components;

namespace Core.Containers;

public class EntityPool : IDisposable
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

    public void Dispose()
    {
        foreach (var entity in Entities)
        {
            entity.Dispose();
        }
    }
}