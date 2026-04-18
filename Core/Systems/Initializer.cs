using Core.Components;
using Core.Interfaces;
using Core.World;

namespace Core.Systems;

public class Initializer(Space space)
{
    public List<Task> InitializeDependencies()
    {
        List<Task> result = new List<Task>();
        foreach (var entity in space.Entities)
        {
            foreach (var logic in entity.Logics)
            {
                result.Add(InitializeLogic(logic));
            }
        }
        return result;
    }

    public async Task InitializeLogic(Logic logic)
    {
        (logic as IInitializable)?.Initialize();
    }
}