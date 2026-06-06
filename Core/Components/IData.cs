using Solas.Interfaces;

namespace Solas.Components;

public interface IData : IInjectable, IDisposable
{
    void Write(BinaryWriter writer, Entity entity);

    (Guid, Guid)[] SerializationGuids { get; }

    void IDisposable.Dispose() { }
}