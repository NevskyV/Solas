using Orbitality.Components;
using Orbitality.World;

namespace Orbitality.Systems;

public class DestroySystem
{
    public void DestroyIn(Space space)
    {
        var entities =  Engine.GetEntitiesIn(space).ToArray();
        foreach (var entity in entities) 
            DestroyEntity(entity);
    }

    private void DestroyEntity(Entity entity)
    {
        Engine.Context.EntityPool.UnregisterEntity(entity);
        entity.Dispose();
    }
}