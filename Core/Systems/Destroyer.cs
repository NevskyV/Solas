using Core.Components;

namespace Core.Systems;

public class Destroyer
{
    public void DestroyAll()
    {
        var allEntities = Engine.GetEntities(Engine.WorldContext.GlobalSpace).ToList();
        foreach (var space in Engine.WorldContext.LocalSpaces)
        {
            allEntities.AddRange(Engine.GetEntities(space));
        }

        allEntities.RemoveAll(x => x == null);
        foreach (var entity in allEntities)
        {
            DestroyEntity(entity);
        }
    }
    
    public void DestroyEntity(Entity entity)
    {
        Engine.Context.EntityPool.UnregisterEntity(entity);
        entity.Dispose();
    }
}