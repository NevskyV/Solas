using Core.Components;

namespace Core.Systems;

public static class Destroyer
{
    public static void DestroyAll()
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
    
    public static void DestroyEntity(Entity entity)
    {
        Engine.AppContext.EntityPool.UnregisterEntityById(entity.Id);
        entity.Dispose();
    }
}