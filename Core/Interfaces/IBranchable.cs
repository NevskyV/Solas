namespace Orbitality.Interfaces;

public interface IBranchable
{
    public Guid Id { get; set; }
    public Guid RootId { get; set; }
    public List<Guid> BranchesIds { get; set; }

    public IBranchable GetRoot();
    
    public IEnumerable<IBranchable> GetBranches();
}