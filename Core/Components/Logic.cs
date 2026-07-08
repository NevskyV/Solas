using Solas.Interfaces;

namespace Solas.Components;

public abstract class Logic : IInjectable, IDisposable
{
    public Entity Entity { get; internal init; }

    public virtual void Dispose()
    {
    }

    public virtual void WriteInject(FileStream stream, Entity entity)
    {
    }

    public virtual (Guid, Guid)[] ReadInject(FileStream stream)
    {
        return null;
    }

    public virtual void Inject((Guid, Guid)[] guids)
    {
    }
}