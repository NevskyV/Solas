namespace Solas.Interfaces;

public interface IBranchable : IReferenceable
{
    public Guid RootId { get; set; }
    public List<Guid> BranchesIds { get; set; }

    public IBranchable GetRoot();

    public IEnumerable<IBranchable> GetBranches();
}