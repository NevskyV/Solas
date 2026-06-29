using Solas.Components;
using Solas.Containers;
using Solas.Enums;
using Solas.Serialization.Core;
using Solas.World;

namespace Solas.Serialization.CustomSerializers;

public class SpaceSerializer : ICustomSerializer<Space>
{
    public void Write(Space value, FileStream stream, Serializer serializer, string name = null)
    {
        serializer.Open(stream);
        serializer.Write(value.Id, stream, "SpaceId");
        serializer.Write(value.RootId, stream,  "RootId");

        // Initialization pool
        var pool = value.Initializer.Pool;
        serializer.BeginObject(stream, "InitializationPool");
        serializer.Write((ushort)pool.OrderType, stream, "OrderType");
        serializer.WriteArray(pool.OrderedEntitiesIds, stream, EngineContext.Serializer.Write, "OrderedEntitiesIds");
        serializer.EndObject(stream);

        // SpaceFolders
        serializer.WriteArray(Query.GetAllSpaceFoldersIn(value).ToArray(), stream, name: "SpaceFolders");

        // Entities
        serializer.WriteArray(Query.GetEntitiesIn(value).ToArray(), stream, name: "Entities");

        serializer.Close(stream);
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