namespace Core.Components;

public abstract class EntityModificator
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