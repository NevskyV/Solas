namespace Orbitality.Components;

public abstract class EntityModifier(Entity entity)
{
    public bool IsEnabled
    {
        get;
        set
        {
            field = value;
            if (value) OnEnable();
            else OnDisable();
        }
    } = false;

    protected abstract void OnEnable();
    protected abstract void OnDisable();
}