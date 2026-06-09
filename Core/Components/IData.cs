using Solas.Interfaces;

namespace Solas.Components;

public interface IData : IInjectable, IDisposable
{
    (Guid, Guid)[] SerializationGuids { get; }

    void IDisposable.Dispose()
    {
    }

    void Write(BinaryWriter writer, Entity entity);
}