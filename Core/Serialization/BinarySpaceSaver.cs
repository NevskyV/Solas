using Orbitality.Components;
using Orbitality.Containers;
using Orbitality.Systems;
using Orbitality.World;

namespace Orbitality.Serialization;

public static class BinarySpaceSaver
{
    public static void SaveSpace(Space space, Entity[] entities)
    {
        using var stream = File.Open(space.Path, FileMode.OpenOrCreate, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        writer.Write(space.Id.ToByteArray());
        writer.Write(space.RootId.ToByteArray());
        
        // =========================
        // Initialization pool
        // =========================
        
        var pool = space.Initializer.Pool;
        writer.Write((ushort)pool.OrderType);

        writer.Write(pool.OrderedEntitiesIds.Length);

        foreach (var guid in pool.OrderedEntitiesIds)
        {
            writer.Write(guid.ToByteArray());
        }

        // =========================
        // Entities
        // =========================

        writer.Write(entities.Length);

        foreach (var entity in entities)
        {
            writer.Write(entity.Id.ToByteArray());

            writer.Write(entity.MetaData.Name ?? string.Empty);
            writer.Write(entity.MetaData.Tag ?? string.Empty);
            writer.Write(entity.MetaData.Icon);

            // =========================
            // Data
            // =========================

            writer.Write(entity.Data.Length);

            foreach (var data in entity.Data)
            {
                var type = data.GetType();
                writer.Write($"{type.FullName}, {type.Assembly.GetName().Name}");

                var method = type.GetMethod("Write")!;
                
                method.Invoke(data, [writer, data]);
            }

            // =========================
            // Logic
            // =========================

            writer.Write(entity.Logics.Length);

            foreach (var logic in entity.Logics)
            {
                var type = logic.GetType();
                writer.Write($"{type.FullName}, {type.Assembly.GetName().Name}");
            }
        }
    }

    public static InitializationPool LoadSpace(Space space, string path)
    {
        var pool = new InitializationPool();
        if (!File.Exists(path) || File.ReadAllBytes(path).Length == 0) return pool;
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream);

        space.Id = new Guid(reader.ReadBytes(16));
        space.RootId = new Guid(reader.ReadBytes(16));
        
        // =========================
        // Initialization pool
        // =========================

        pool.OrderType = (InitializationOrder)reader.ReadUInt16();

        int orderedCount = reader.ReadInt32();

        var ordered = new Guid[orderedCount];

        for (int i = 0; i < orderedCount; i++)
        {
            ordered[i] = new Guid(reader.ReadBytes(16));
        }

        pool.OrderedEntitiesIds = ordered;

        // =========================
        // Entities
        // =========================

        int entityCount = reader.ReadInt32();

        for (int i = 0; i < entityCount; i++)
        {
            var id = new Guid(reader.ReadBytes(16));

            var metaData = new EntityMetaData(
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadUInt16());

            var entity =  new Entity(id, space, metaData);

            // =========================
            // Data
            // =========================

            int dataCount = reader.ReadInt32();

            for (int j = 0; j < dataCount; j++)
            {
                var typeName = reader.ReadString();

                var type = Type.GetType(typeName)!;
                var method = type.GetMethod("Read")!;
                var data = method.Invoke(new(), [reader]);
                entity.AddData((IData)data);
            }

            // =========================
            // Logic
            // =========================

            int logicCount = reader.ReadInt32();

            for (int j = 0; j < logicCount; j++)
            {
                var typeName = reader.ReadString();

                var type = Type.GetType(typeName)!;

                var method = typeof(Entity)
                    .GetMethod(nameof(Entity.AddLogic))!
                    .MakeGenericMethod(type);

                method.Invoke(entity, null);
            }
        }

        return pool;
    }
}