using Orbitality.Components;
using Orbitality.Interfaces;
using Orbitality.World;

namespace Orbitality.Systems;

public class Initializer(Space space)
{
    public List<Task> InitializeDependencies()
    {
        var result = new List<Task>();
        foreach (var entity in Engine.GetEntities(space))
        foreach (var logic in entity.Logics)
            result.Add(InitializeLogic(logic));

        return result;
    }

    public async Task InitializeLogic(Logic logic)
    {
        (logic as IInitializable)?.Initialize();
    }
}