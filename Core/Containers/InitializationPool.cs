using Orbitality.Systems;

namespace Orbitality.Containers;

public struct InitializationPool()
{
    public InitializationOrder OrderType = InitializationOrder.Random;
    public Guid[] OrderedEntitiesIds = [];
}