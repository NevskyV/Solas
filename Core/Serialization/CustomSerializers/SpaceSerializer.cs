using Solas.Components;
using Solas.Containers;
using Solas.Enums;
using Solas.Serialization.Core;
using Solas.World;

namespace Solas.Serialization.CustomSerializers;

public class SpaceSerializer : ICustomSerializer<Space>
{
    public void Write(Space value, FileStream stream, string name = null)
    {
        EngineContext.Serializer.Open(stream);
        EngineContext.Serializer.Write(value.Id, stream);
        EngineContext.Serializer.Write(value.RootId, stream);

        // Initialization pool
        var pool = value.Initializer.Pool;
        EngineContext.Serializer.BeginObject(stream, "InitializationPool");
        EngineContext.Serializer.Write((ushort)pool.OrderType, stream);
        EngineContext.Serializer.WriteArray(pool.OrderedEntitiesIds, stream, EngineContext.Serializer.Write);
        EngineContext.Serializer.EndObject(stream);

        // SpaceFolders
        EngineContext.Serializer.WriteArray(Query.GetAllSpaceFoldersIn(value).ToArray(), stream, name: "SpaceFolders");

        // Entities
        EngineContext.Serializer.WriteArray(Query.GetEntitiesIn(value).ToArray(), stream, name: "Entities");

        EngineContext.Serializer.Close(stream);
    }

    public Space Read(FileStream stream)
    {
        EngineContext.Serializer.Open(stream);
        var id = EngineContext.Serializer.ReadGuid(stream);
        var space = new Space(id)
        {
            RootId = EngineContext.Serializer.ReadGuid(stream)
        };

        // Initialization pool
        var pool = new InitializationPool
        {
            OrderType = (InitializationOrder)EngineContext.Serializer.ReadUInt16(stream)
        };

        var ordered = EngineContext.Serializer.ReadArray(stream, EngineContext.Serializer.ReadGuid);

        pool.OrderedEntitiesIds = ordered;
        space.Initializer.Pool = pool;

        // SpaceFolders
        EngineContext.Serializer.ReadArray<SpaceFolder>(stream);

        // Entities
        var entities = EngineContext.Serializer.ReadArray<Entity>(stream);
        foreach (var entity in entities)
            entity.CurrentSpace = space;

        EngineContext.Serializer.Close(stream);
        return space;
    }
}