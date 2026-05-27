using Orbitality.Interfaces;
using Orbitality.Systems;

namespace Orbitality.World;

public class Space : IBranchable
{
    public string Name { get; }
    public string Path { get; }

    public Guid Id
    {
        get;
        set
        {
            if (field == Guid.Empty)
                field = value;
        }
    }

    public Guid RootId { get; set; }
    public List<Guid> BranchesIds { get; set; }
    public readonly Initializer Initializer;

    public Space(string name, string path)
    {
        Name = name;
        Path = path;
        Initializer = new Initializer(this);
        Engine.Context.EntityPool.RegisterSpace(this);
        Engine.Context.Destroyer.AddSpace(this);
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