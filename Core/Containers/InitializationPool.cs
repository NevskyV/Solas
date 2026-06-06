namespace Solas.Containers;

public enum InitializationOrder { Random, Prefixal, Suffixal, Custom }

public struct InitializationPool()
{
    public InitializationOrder OrderType = InitializationOrder.Random;
    public Guid[] OrderedEntitiesIds = [];
}