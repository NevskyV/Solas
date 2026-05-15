using Orbitality.Interfaces;

namespace Orbitality.Containers;

public struct SpaceFolder() : IBranchable
{
    public Guid RootId { get; set; }
    public List<Guid> BranchesIds { get; set; }
    public Guid Guid { get; init; } = Guid.NewGuid();
    private List<Guid> EntityIds { get; init; } = [];
    
    public void AddEntityId(Guid entityId) => EntityIds.Add(entityId);
    public void RemoveEntityId(Guid entityId) => EntityIds.Remove(entityId);
    public Guid[] GetEntityIds() => EntityIds.ToArray();
    
    public IBranchable GetRoot()
    {
        return Engine.Context.EntityPool.GetSpaceFolderWith(RootId);
    }
    
    public IEnumerable<IBranchable> GetBranches()
    {
        return Engine.Context.EntityPool.GetSpaceFoldersWith(BranchesIds).Cast<IBranchable>();
    }
}