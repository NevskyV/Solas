using Core.Components;

namespace Core.Systems;

public class Creator
{
    public void CreateEntity(EntityMetaData entityMetaData = default)
    {
        Entity newEntity = new Entity(entityMetaData);
        Engine.AppContext.EntityPool.RegisterEntity(newEntity);
    }
}