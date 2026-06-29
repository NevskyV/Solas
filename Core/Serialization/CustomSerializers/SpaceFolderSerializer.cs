using Solas.Serialization.Core;
using Solas.World;

namespace Solas.Serialization.CustomSerializers;

public class SpaceFolderSerializer : ICustomSerializer<SpaceFolder>
{
    public void Write(SpaceFolder value, FileStream stream, Serializer serializer, string name = null)
    {
        serializer.BeginObject(stream, name);
        serializer.Write(value.Id, stream, "Id");
        serializer.Write(value.RootId, stream, "RootId");

        serializer.WriteArray(value.BranchesIds.ToArray(), stream, EngineContext.Serializer.Write, "BranchesIds");
        serializer.WriteArray(value.EntityIds.ToArray(), stream, EngineContext.Serializer.Write, "EntityIds");
        serializer.EndObject(stream);
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