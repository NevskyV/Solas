namespace Solas.ComponentUtils;

public static class ComponentRegistry
{
    private static readonly Dictionary<Type, int> _typeIndices = new();
    public static int Count { get; private set; }

    public static int GetId(Type type)
    {
        if (!_typeIndices.TryGetValue(type, out var id))
        {
            id = Count++;
            _typeIndices[type] = id;
        }

        return id;
    }
}