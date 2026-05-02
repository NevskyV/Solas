using Orbitality.ComponentUtils;

namespace Orbitality.Components;

public abstract partial class Logic
{
    protected IServiceProvider ServiceProvider { get; private set; }
    protected Entity Entity { get; private set; }

    private bool _isActive;

    public ReactiveProperty<bool> IsEnabled
    {
        get;
        set
        {
            _isActive = value.Value;
            field.Value = _isActive && Entity.IsEnabled.Value;
        }
    } = new(false);

    public void SetupLogic(Entity entity, IServiceProvider provider)
    {
        Entity = entity;
        ServiceProvider = provider;

        Entity.IsEnabled.Subscribe(OnEntityEnableChange);

        ResolveDependencies();
    }

    public void OnEntityEnableChange(bool _)
    {
        IsEnabled = new ReactiveProperty<bool>(_isActive);
    }

    partial void ResolveDependencies();
}