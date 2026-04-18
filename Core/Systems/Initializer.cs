using Core.Components;
using Core.Interfaces;
using Core.World;

namespace Core.Systems;

public class Initializer
{
    public void InitializeDependencies(Space space)
    {
        foreach (var entity in space.Entities)
        {
            foreach (var logic in entity.Logics)
            {
                InitializeLogic(logic);
            }
        }
    }

    public void InitializeLogic(Logic logic)
    {
        (logic as IInitializable)?.OnInitialize();
    }
}