namespace Core.Components;

public class Entity(EntityMetaData metaData)
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public EntityMetaData MetaData { get; set; } = metaData;
    private List<IState> States { get; } = [];
    private List<Behavior> Behaviors { get; } = [];

    public void AddState(IState state)
    {
        States.Add(state);
    }

    public void RemoveState(IState state)
    {
        States.Remove(state);
    }

    public void AddBehavior(Behavior behavior)
    {
        Behaviors.Add(behavior);
    }

    public void RemoveBehavior(Behavior behavior)
    {
        Behaviors.Remove(behavior);
    }
}