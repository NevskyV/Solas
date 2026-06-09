using Solas.Components;
using Solas.Containers;
using Solas.Enums;
using Solas.Interfaces;
using Solas.Systems;

namespace Solas.World;

public class Space : IBranchable
{
    internal readonly InitializeSystem Initializer;

    public Space(string name, string path, Guid id)
    {
        Name = name;
        Path = path;
        Id = id;
        Initializer = new InitializeSystem(this);
        EngineContext.EntityPool.RegisterSpace(this);
    }

    public string Name { get; }
    public string Path { get; }

    public Guid Id { get; init; }
    public Guid RootId { get; set; }
    public List<Guid> BranchesIds { get; set; } = [];

    public IBranchable GetRoot()
    {
        return RootId == Guid.Empty ? WorldContext.GlobalSpace : Query.GetSpace(RootId);
    }

    public IEnumerable<IBranchable> GetBranches()
    {
        return BranchesIds.Select(Query.GetSpace);
    }

    public Guid GetSpaceId()
    {
        return Id;
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(Id.ToByteArray());
        writer.Write(RootId.ToByteArray());

        // =========================
        // Initialization pool
        // =========================

        var pool = Initializer.Pool;
        writer.Write((ushort)pool.OrderType);

        writer.Write(pool.OrderedEntitiesIds.Length);

        foreach (var guid in pool.OrderedEntitiesIds) writer.Write(guid.ToByteArray());

        // =========================
        // SpaceFolders
        // =========================

        var folders = Query.GetAllSpaceFoldersIn(this);
        writer.Write(folders.Count);
        foreach (var folder in folders) folder.Write(writer);

        // =========================
        // Entities
        // =========================

        var entities = Query.GetEntitiesIn(this).ToArray();

        writer.Write(entities.Length);

        foreach (var entity in entities) entity.Write(writer);
    }

    public IReferenceable Read(BinaryReader reader)
    {
        RootId = new Guid(reader.ReadBytes(16));

        // =========================
        // Initialization pool
        // =========================

        var pool = new InitializationPool
        {
            OrderType = (InitializationOrder)reader.ReadUInt16()
        };

        var orderedCount = reader.ReadInt32();

        var ordered = new Guid[orderedCount];

        for (var i = 0; i < orderedCount; i++) ordered[i] = new Guid(reader.ReadBytes(16));

        pool.OrderedEntitiesIds = ordered;
        Initializer.Pool = pool;

        // =========================
        // SpaceFolders
        // =========================

        var foldersCount = reader.ReadInt32();
        for (var i = 0; i < foldersCount; i++)
        {
            var folder = new SpaceFolder(new Guid(reader.ReadBytes(16)), this);
            folder.Read(reader);
        }

        // =========================
        // Entities
        // =========================

        var entityCount = reader.ReadInt32();

        for (var i = 0; i < entityCount; i++) Entity.StaticRead(reader, this);

        return this;
    }
}