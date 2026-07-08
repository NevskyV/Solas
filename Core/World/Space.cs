using Solas.Interfaces;
using Solas.Systems;

namespace Solas.World;

public class Space : IBranchable, IReferenceable
{
    internal readonly InitializeSystem Initializer;

    public Space(Guid id)
    {
        Id = id;
        Initializer = new InitializeSystem(this);
        EngineContext.EntityPool.RegisterSpace(this);
    }

    public string Name { get; set; }
    public string Path { get; set; }

    public Guid Id { get; init; }
    public Guid RootId { get; set; }
    public List<Guid> BranchesIds { get; set; } = [];

    public IBranchable GetRoot()
    {
        return RootId == Guid.Empty ? WorldContext.GlobalSpace : EngineContext.SpacePool.GetSpace(RootId);
    }

    public IEnumerable<IBranchable> GetBranches()
    {
        return BranchesIds.Select(EngineContext.SpacePool.GetSpace);
    }

    public Guid GetSpaceId()
    {
        return Guid.Empty;
    }

    public static IReferenceable SearchReferenceable<T>(Guid id, Guid spaceId) where T : class, IReferenceable
    {
        return EngineContext.SpacePool.GetSpace(id);
    }
}