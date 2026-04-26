namespace Orbitality.Components;

public static class ComponentRegistry
{
    private static readonly Dictionary<Type, int> _typeIndices = new();
    private static int _nextIndex = 0;
    public static int Count => _nextIndex;

    public static int GetId(Type type)
    {
        if (!_typeIndices.TryGetValue(type, out var id))
        {
            id = _nextIndex++;
            _typeIndices[type] = id;
        }

        return id;
    }
}