namespace Solas.ComponentUtils;

internal static class ComponentRegistry
{
    private static readonly Dictionary<Type, int> _typeIndices = new();
    internal static int Count { get; private set; }

    internal static int GetId(Type type)
    {
        if (!_typeIndices.TryGetValue(type, out var id))
        {
            id = Count++;
            _typeIndices[type] = id;
        }

        return id;
    }
}