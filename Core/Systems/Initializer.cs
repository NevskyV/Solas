using Orbitality.Components;
using Orbitality.Interfaces;
using Orbitality.World;

namespace Orbitality.Systems;

public class Initializer(Space space)
{
    public List<Task> InitializeDependencies()
    {
        var result = new List<Task>();
        foreach (var entity in Engine.GetEntities(space)) result.AddRange(entity.Logics.Select(InitializeLogic));

        return result;
    }

    private Task InitializeLogic(Logic logic)
    {
        try
        {
            (logic as IInitializable)?.Initialize();
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }
}