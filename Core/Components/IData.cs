using Solas.Interfaces;

namespace Solas.Components;

public interface IData : IInjectable
{
    void Write(BinaryWriter writer, Entity entity);

    (Guid, Guid)[] SerializationGuids { get; }
}