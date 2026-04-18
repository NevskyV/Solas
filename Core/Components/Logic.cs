namespace Core.Components;

public abstract partial class Logic
{
    protected IServiceProvider ServiceProvider { get; private set; }
    protected Entity Entity { get; private set; }

    public void SetupLogic(Entity entity, IServiceProvider provider)
    {
        Entity = entity;
        ServiceProvider = provider;
        ResolveDependencies();
    }
    
    partial void ResolveDependencies();
}