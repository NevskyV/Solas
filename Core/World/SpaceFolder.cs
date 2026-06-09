using System.Runtime.InteropServices;
using Solas.Interfaces;

namespace Solas.World;

public class SpaceFolder : IBranchable
{
    public SpaceFolder(Guid id, Space space)
    {
        Id = id;
        Space = space;
        EngineContext.SpacePool.RegisterSpaceFolder(this, space);
    }

    private List<Guid> EntityIds { get; } = [];
    public Space Space { get; init; }
    public Guid Id { get; init; }
    public Guid RootId { get; set; }
    public List<Guid> BranchesIds { get; set; }

    public IBranchable GetRoot()
    {
        return Query.GetSpaceFolderWith(RootId, Space);
    }

    public IEnumerable<IBranchable> GetBranches()
    {
        return Query.GetSpaceFoldersWith(BranchesIds, Space);
    }

    public Guid GetSpaceId()
    {
        return Space.Id;
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(Id.ToByteArray());
        writer.Write(RootId.ToByteArray());

        writer.Write(BranchesIds.Count);
        foreach (var branchId in BranchesIds) writer.Write(branchId.ToByteArray());

        var entityIds = GetEntityIds();
        writer.Write(entityIds.Length);
        foreach (var entityId in entityIds) writer.Write(entityId.ToByteArray());
    }

    public IReferenceable Read(BinaryReader reader)
    {
        var folderRootId = new Guid(reader.ReadBytes(16));

        var folderBranchesCount = reader.ReadInt32();
        var folderBranches = new Guid[folderBranchesCount];
        for (var j = 0; j < folderBranchesCount; j++) folderBranches[j] = new Guid(reader.ReadBytes(16));

        RootId = folderRootId;
        BranchesIds = folderBranches.ToList();

        var entityIdsCount = reader.ReadInt32();
        for (var j = 0; j < entityIdsCount; j++) AddEntityId(new Guid(reader.ReadBytes(16)));
        return this;
    }

    public void AddEntityId(Guid entityId)
    {
        EntityIds.Add(entityId);
    }

    public void RemoveEntityId(Guid entityId)
    {
        EntityIds.Remove(entityId);
    }

    public ReadOnlySpan<Guid> GetEntityIds()
    {
        return CollectionsMarshal.AsSpan(EntityIds);
    }
}