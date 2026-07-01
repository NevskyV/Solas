using Solas.Interfaces;

namespace Solas.World;

public class SpaceFolder : IBranchable, IDisposable
{
    public List<Guid> EntityIds { get; init; } = [];

    public SpaceFolder(Guid id = default, Space space = null)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Space = space ?? WorldContext.GlobalSpace;
    }
    
    public Space Space
    {
        get;
        set
        {
            if (field == value)
                return;
            if(field != null)
                EngineContext.SpacePool.UnregisterSpaceFolder(this, field);
            field = value;
            if(field != null)
                EngineContext.SpacePool.RegisterSpaceFolder(this, field);
        }
    }

    public Guid Id { get; init; }
    public Guid RootId { get; set; }
    public List<Guid> BranchesIds { get; set; } = [];

    public IBranchable GetRoot()
    {
        return RootId == Guid.Empty ? null : Query.GetSpaceFolderWith(RootId, Space);
    }

    public IEnumerable<IBranchable> GetBranches()
    {
        return Query.GetSpaceFoldersWith(BranchesIds, Space);
    }

    public Guid GetSpaceId()
    {
        return Space.Id;
    }

    public static IReferenceable SearchReferenceable<T>(Guid id, Guid spaceId) where T : class, IReferenceable
    {
        return EngineContext.SpacePool.GetSpaceFolderWith(id, spaceId);
    }

    public void Dispose()
    {
        Space = null;
        EntityIds.Clear();
        RootId = Guid.Empty;
    }
}