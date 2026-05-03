using Orbitality.Components;
using Orbitality.World;

namespace Orbitality.Systems;

public class Destroyer
{
    private readonly HashSet<Space> _loadedSpaces = [];
    public void AddSpace(Space space) => _loadedSpaces.Add(space);

    public void DestroyIn(Space space)
    {
        var entities =  Engine.GetEntitiesIn(space).ToArray();
        foreach (var entity in entities) 
            DestroyEntity(entity);
    }
    
    public void DestroyAll()
    {
        foreach (var loadedSpace in _loadedSpaces)
        {
            DestroyIn(loadedSpace);
        }
    }

    private void DestroyEntity(Entity entity)
    {
        Engine.Context.EntityPool.UnregisterEntity(entity);
        entity.Dispose();
    }
}