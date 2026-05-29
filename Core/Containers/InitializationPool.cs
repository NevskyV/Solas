using Solas.Systems;

namespace Solas.Containers;

public struct InitializationPool()
{
    public InitializationOrder OrderType = InitializationOrder.Random;
    public Guid[] OrderedEntitiesIds = [];
}