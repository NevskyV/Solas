using Solas.Interfaces;

namespace Solas.Components;

public abstract class Logic : IInjectable
{
    public Entity Entity { get; init; }
}