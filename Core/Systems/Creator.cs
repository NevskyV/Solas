using Core.Components;
using Core.World;

namespace Core.Systems;

public class Creator
{
    public void CreateAndWriteEntitiesToSpace(Space space)
    {
        
    }
    
    public Entity CreateEntity(Space space = null, EntityMetaData entityMetaData = default)
    {
        space ??= Engine.WorldContext.GlobalSpace;
        entityMetaData = entityMetaData == default? EntityMetaData.CreateDefault() : entityMetaData;
        Entity newEntity = new Entity(space, entityMetaData);
        Engine.AppContext.EntityPool.RegisterEntity(newEntity);
        return newEntity;
    }
}