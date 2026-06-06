using Solas.Interfaces;

namespace Solas.Components;

public abstract class Logic : IInjectable, IDisposable
{
    public Entity Entity { get; init; }
    public virtual void Dispose(){}
}