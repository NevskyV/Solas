namespace Orbitality.Components;

public class RecordableModifier(Entity entity) : EntityModifier(entity)
{
    protected override void OnEnable()
    {
    }

    protected override void OnDisable()
    {
        //Engine.Context.SpaceSystem.SaveEntity(entity);
    }
}