using Solas.ComponentUtils;
using Solas.Serialization.Core;

namespace Solas.Serialization.CustomSerializers;

public class EntityMetaDataSerializer : ICustomSerializer<EntityMetaData>
{
    public void Write(EntityMetaData value, FileStream stream, string name = null)
    {
        EngineContext.Serializer.BeginObject(stream, name);
        EngineContext.Serializer.Write(value.Name, stream, nameof(value.Name));
        EngineContext.Serializer.Write(value.Tag, stream, nameof(value.Tag));
        EngineContext.Serializer.Write(value.Icon, stream, nameof(value.Icon));
        EngineContext.Serializer.EndObject(stream);
    }

    public EntityMetaData Read(FileStream stream)
    {
        return new EntityMetaData(
            EngineContext.Serializer.ReadString(stream),
            EngineContext.Serializer.ReadString(stream),
            EngineContext.Serializer.ReadUInt16(stream));
    }
}