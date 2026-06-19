using Solas.Components;
using Solas.Generated;

namespace Solas.Registries;

public sealed class LogicAddingRegistry
{
    private readonly Dictionary<string, Func<Entity, Logic>> _readers = [];

    public LogicAddingRegistry()
    {
        LogicAddingRegistration.Add(this);
    }

    public void Register<T>(string typeName) where T : Logic, new()
    {
        _readers[typeName] = entity => entity.AddLogic<T>();
    }

    internal Logic AddLogic(string typeName, Entity entity)
    {
        if (!_readers.TryGetValue(typeName, out var func))
            throw new InvalidOperationException(
                $"Type '{typeName}' is not registered for binary deserialization.");

        return func(entity);
    }
}