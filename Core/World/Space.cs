using Solas.Interfaces;
using Solas.Systems;

namespace Solas.World;

public class Space : IBranchable
{
    public string Name { get; }
    public string Path { get; }

    public Guid Id { get; init; }
    public Guid RootId { get; set; }
    public List<Guid> BranchesIds { get; set; } = [];
    public readonly InitializeSystem Initializer;

    public Space(string name, string path, Guid id)
    {
        Name = name;
        Path = path;
        Id = id;
        Initializer = new InitializeSystem(this);
        Engine.Context.EntityPool.RegisterSpace(this);
    }
    
    public IBranchable GetRoot()
    {
        return RootId == Guid.Empty ? Engine.GlobalSpace : Engine.Context.SpacePool.GetSpace(RootId);
    }

    public IEnumerable<IBranchable> GetBranches()
    {
        return BranchesIds.Select(x => Engine.Context.SpacePool.GetSpace(x));
    }
}