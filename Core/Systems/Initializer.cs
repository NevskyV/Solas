using Orbitality.Components;
using Orbitality.Containers;
using Orbitality.Interfaces;
using Orbitality.World;

namespace Orbitality.Systems;

public class Initializer(Space space)
{
    public SpaceContainer Container;
    
    public IEnumerable<Task> InitializeDependencies()
    {
        var entities = Engine.GetEntitiesIn(space).ToArray();
        var allTasks = entities.SelectMany(entity => entity.Logics.Select(InitializeLogic));
        return allTasks;
    }

    private async Task InitializeLogic(Logic logic)
    {
        await Task.Run(((IInitializable)logic).Initialize);
    }
}

public enum InitializationOrder { Random, Prefixal, Suffixal, Custom }