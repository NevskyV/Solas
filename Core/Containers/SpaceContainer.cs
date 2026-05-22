using Orbitality.Systems;

namespace Orbitality.Containers;

public struct SpaceContainer()
{
    public InitializationOrder OrderType = InitializationOrder.Random;
    public Guid[] OrderedEntitiesIds = [];
}