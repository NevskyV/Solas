using Orbitality.Components;
using Orbitality.World;

namespace Orbitality.Systems;

public class Creator
{
    public Entity CreateEntity(Guid id = default, Space space = null, EntityMetaData entityMetaData = default)
    {
        //Set default values
        space ??= Engine.GlobalSpace;
        entityMetaData = entityMetaData == default ? EntityMetaData.CreateDefault() : entityMetaData;
        id = id == Guid.Empty ? Guid.NewGuid() : id;
        
        //Create Entity
        var newEntity = new Entity(id, space, entityMetaData);

        //Register & return
        Engine.Context.EntityPool.RegisterEntity(newEntity);
        return newEntity;
    }
}