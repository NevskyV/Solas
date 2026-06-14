using Solas.Interfaces;
using Solas.Systems;

namespace Solas.World;

public class Space : IBranchable
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
        return RootId == Guid.Empty ? WorldContext.GlobalSpace : Query.GetSpace(RootId);
    }

    public IEnumerable<IBranchable> GetBranches()
    {
        return BranchesIds.Select(Query.GetSpace);
    }

    public Guid GetSpaceId() => Guid.Empty;
    public static IReferenceable SearchReferenceable(Guid id, Guid spaceId) => EngineContext.SpacePool.GetSpace(id);
}