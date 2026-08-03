using System.Numerics;
using Solas.ComponentUtils;
using Solas.Components;
using Solas.Interfaces;

namespace Solas.Transform;

public class TransformData(Vector3 position = new(), Vector3 rotation = new(), Vector3 scale = new())
    : IData, IBranchable
{
    public Entity Entity
    {
        get;
        set
        {
            field = value;
            TransformEventHandler.RegisterData(this);
        }
    }

    public DataProperty<Vector3> Position = new() { Value = position };
    public DataProperty<Vector3> Rotation = new() { Value = rotation };
    public DataProperty<Vector3> Scale = new() { Value = scale };

    public Guid RootId { get; set; }
    public List<Guid> BranchesIds { get; set; } = [];

    void IDisposable.Dispose() => TransformEventHandler.UnregisterData(this);

    public IBranchable GetRoot()
    {
        return Query.GetEntitiesInAvailable(Entity.CurrentSpace).FirstOrDefault(x => x.Id == RootId)
            .GetData<TransformData>();
    }

    public IEnumerable<IBranchable> GetBranches()
    {
        var result = new List<IBranchable>();
        var space = Entity.CurrentSpace;
        foreach (var branchId in BranchesIds)
        {
            result.Add(Query.GetEntitiesInAvailable(space).FirstOrDefault(x => x.Id == branchId)
                .GetData<TransformData>());
        }

        return result;
    }
}