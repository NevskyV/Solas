using Solas.World;

namespace Solas.Systems;

internal class DestroySystem
{
    internal void DestroyIn(Space space)
    {
        var entities = Query.GetEntitiesIn(space).ToArray();
        foreach (var entity in entities)
            entity.Dispose();
    }
}