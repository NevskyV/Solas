using Solas.Components;
using Solas.World;

namespace Solas.Systems;

internal class DestroySystem
{
    internal void DestroyIn(Space space)
    {
        var entities =  Engine.GetEntitiesIn(space).ToArray();
        foreach (var entity in entities) 
            DestroyEntity(entity);
    }

    internal void DestroyEntity(Entity entity)
    {
        EngineContext.EntityPool.UnregisterEntity(entity);
        entity.Dispose();
    }
}