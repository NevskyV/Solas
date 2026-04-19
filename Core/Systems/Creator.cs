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
        //Set default values
        space ??= Engine.WorldContext.GlobalSpace;
        entityMetaData = entityMetaData == default? EntityMetaData.CreateDefault() : entityMetaData;
        
        //Create Entity
        Entity newEntity = new Entity(space, entityMetaData);
        
        //Register & return
        space.Entities.Add(newEntity);
        Engine.AppContext.EntityPool.RegisterEntity(newEntity);
        return newEntity;
    }
}