using Core.Components;

namespace Core.Systems;

public class Destroyer
{
    public void DestroyEntity(Entity entity)
    {
        entity.Dispose();
        Engine.AppContext.EntityPool.UnregisterEntityById(entity.Id);
    }
}