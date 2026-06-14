using Solas.Interfaces;

namespace Solas.World;

public class SpaceFolder(Guid id) : IBranchable
{
    public List<Guid> EntityIds { get; init; } = [];

    public Space Space
    {
        get;
        set
        {
            EngineContext.SpacePool.UnregisterSpaceFolder(this, field);
            field = value;
            EngineContext.SpacePool.RegisterSpaceFolder(this, field);
        }
    }

    public Guid Id { get; init; } = id;
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

    public Guid GetSpaceId() => Space.Id;

    public static IReferenceable SearchReferenceable(Guid id, Guid spaceId) => EngineContext.SpacePool.GetSpaceFolderWith(id, spaceId);
}