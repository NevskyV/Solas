using Orbitality.ComponentUtils;
using Orbitality.Interfaces;

namespace Orbitality.Components;

public class HeavyLogic : Logic, IInitializable, IUpdatable, IFixedUpdatable, ILateUpdatable, IDestroyable, IToggleable
{
    public ReactiveProperty<bool> IsEnabled { get; set; }
    
    public virtual void Initialize()
    {
    }

    public virtual void Update()
    {
    }

    public virtual void FixedUpdate()
    {
    }

    public virtual void LateUpdate()
    {
    }

    public virtual void Destroy()
    {
    }
}