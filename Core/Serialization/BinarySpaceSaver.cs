using Solas.Components;
using Solas.Containers;
using Solas.Systems;
using Solas.World;

namespace Solas.Serialization;

public static class BinarySpaceSaver
{
    public static void SaveSpace(Space space)
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
        // SpaceFolders
        // =========================

        var folders = Engine.Context.SpacePool.GetAllSpaceFoldersIn(space);
        writer.Write(folders.Count);
        foreach (var folder in folders)
        {
            writer.Write(folder.Id.ToByteArray());
            writer.Write(folder.RootId.ToByteArray());
            
            writer.Write(folder.BranchesIds.Count);
            foreach (var branchId in folder.BranchesIds)
            {
                writer.Write(branchId.ToByteArray());
            }

            var entityIds = folder.GetEntityIds();
            writer.Write(entityIds.Length);
            foreach (var entityId in entityIds)
            {
                writer.Write(entityId.ToByteArray());
            }
        }

        // =========================
        // Entities
        // =========================
        
        var entities = Engine.GetEntitiesIn(space).ToArray();
        
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
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream);

        reader.ReadBytes(16); //Id already has written
        space.RootId = new Guid(reader.ReadBytes(16));
        
        // =========================
        // Initialization pool
        // =========================
        
        var pool = new InitializationPool
        {
            OrderType = (InitializationOrder)reader.ReadUInt16()
        };

        var orderedCount = reader.ReadInt32();

        var ordered = new Guid[orderedCount];

        for (var i = 0; i < orderedCount; i++)
        {
            ordered[i] = new Guid(reader.ReadBytes(16));
        }

        pool.OrderedEntitiesIds = ordered;
        
        // =========================
        // SpaceFolders
        // =========================

        var foldersCount = reader.ReadInt32();
        for (var i = 0; i < foldersCount; i++)
        {
            var folderId = new Guid(reader.ReadBytes(16));
            var folderRootId = new Guid(reader.ReadBytes(16));
            
            var folderBranchesCount = reader.ReadInt32();
            var folderBranches = new Guid[folderBranchesCount];
            for (var j = 0; j < folderBranchesCount; j++)
            {
                folderBranches[j] = new Guid(reader.ReadBytes(16));
            }

            var folder = new SpaceFolder(folderId, space)
            {
                RootId = folderRootId,
                BranchesIds = folderBranches.ToList(),
            };
            
            var entityIdsCount = reader.ReadInt32();
            for (var j = 0; j < entityIdsCount; j++)
            {
                folder.AddEntityId(new Guid(reader.ReadBytes(16)));
            }
        }

        // =========================
        // Entities
        // =========================

        var entityCount = reader.ReadInt32();

        for (var i = 0; i < entityCount; i++)
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

            var dataCount = reader.ReadInt32();

            for (var j = 0; j < dataCount; j++)
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

            var logicCount = reader.ReadInt32();

            for (var j = 0; j < logicCount; j++)
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