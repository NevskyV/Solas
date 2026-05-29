using System.Runtime.InteropServices;
using Orbitality.Interfaces;

namespace Orbitality.World;

public struct SpaceFolder : IBranchable
{
    public Guid Id { get; init; }
    public Guid RootId { get; set; }
    public List<Guid> BranchesIds { get; set; }
    private List<Guid> EntityIds { get; init; } = [];
    
    public void AddEntityId(Guid entityId) => EntityIds.Add(entityId);
    public void RemoveEntityId(Guid entityId) => EntityIds.Remove(entityId);
    public ReadOnlySpan<Guid> GetEntityIds() => CollectionsMarshal.AsSpan(EntityIds);
    
    public IBranchable GetRoot()
    {
        return Engine.Context.SpacePool.GetSpaceFolderWith(RootId);
    }
    
    public IEnumerable<IBranchable> GetBranches()
    {
        return Engine.Context.SpacePool.GetSpaceFoldersWith(BranchesIds).Cast<IBranchable>();
    }

    public SpaceFolder(Guid id, Space space)
    {
        Id = id;
        Engine.Context.SpacePool.RegisterSpaceFolder(this, space);
    }
}