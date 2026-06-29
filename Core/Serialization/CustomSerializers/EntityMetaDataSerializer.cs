using Solas.ComponentUtils;
using Solas.Serialization.Core;

namespace Solas.Serialization.CustomSerializers;

public class EntityMetaDataSerializer : ICustomSerializer<EntityMetaData>
{
    public void Write(EntityMetaData value, FileStream stream, Serializer serializer, string name = null)
    {
        serializer.BeginObject(stream, name);
        serializer.Write(value.Name, stream, nameof(value.Name));
        serializer.Write(value.Tag, stream, nameof(value.Tag));
        serializer.Write(value.Icon, stream, nameof(value.Icon));
        serializer.EndObject(stream);
    }

    public EntityMetaData Read(FileStream stream)
    {
        return new EntityMetaData(
            EngineContext.Serializer.ReadString(stream),
            EngineContext.Serializer.ReadString(stream),
            EngineContext.Serializer.ReadUInt16(stream));
    }
}