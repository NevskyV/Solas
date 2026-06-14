using Solas.Serialization.Core;
using Solas.World;

namespace Solas.Serialization.CustomSerializers;

public class SpaceFolderSerializer : ICustomSerializer<SpaceFolder>
{
    public void Write(SpaceFolder value, FileStream stream, string name = null)
    {
        EngineContext.Serializer.BeginObject(stream, name);
        EngineContext.Serializer.Write(value.Id, stream);
        EngineContext.Serializer.Write(value.RootId, stream);

        EngineContext.Serializer.WriteArray(value.BranchesIds.ToArray(), stream, EngineContext.Serializer.Write);
        EngineContext.Serializer.WriteArray(value.EntityIds.ToArray(), stream, EngineContext.Serializer.Write);
        EngineContext.Serializer.EndObject(stream);
    }

    public SpaceFolder Read(FileStream stream)
    {
        return new SpaceFolder(EngineContext.Serializer.ReadGuid(stream))
        {
            RootId = EngineContext.Serializer.ReadGuid(stream),
            BranchesIds = EngineContext.Serializer.ReadArray(stream, EngineContext.Serializer.ReadGuid).ToList(),
            EntityIds = EngineContext.Serializer.ReadArray(stream, EngineContext.Serializer.ReadGuid).ToList()
        };
    }
}