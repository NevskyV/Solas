using Orbitality.Components;
using Orbitality.World;

namespace Orbitality.Systems;

public class Creator
{
    public Entity CreateEntity(Space space = null, EntityMetaData entityMetaData = default)
    {
        //Set default values
        space ??= Engine.GlobalSpace;
        entityMetaData = entityMetaData == default ? EntityMetaData.CreateDefault() : entityMetaData;

        //Create Entity
        var newEntity = new Entity(space, entityMetaData);

        //Register & return
        Engine.Context.EntityPool.RegisterEntity(newEntity);
        return newEntity;
    }
}