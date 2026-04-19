using Core.Components;

namespace Core.Systems;

public class Destroyer
{
    public void DestroyAll()
    {
        var allEntities = Engine.WorldContext.GlobalSpace.Entities.ToList();
        foreach (var space in Engine.WorldContext.LocalSpaces)
        {
            allEntities.AddRange(space.Entities);
        }
        
        foreach (var entity in allEntities)
        {
            DestroyEntity(entity);
        }
    }
    
    public void DestroyEntity(Entity entity)
    {
        Engine.AppContext.EntityPool.UnregisterEntityById(entity.Id);
        entity.Dispose();
    }
}