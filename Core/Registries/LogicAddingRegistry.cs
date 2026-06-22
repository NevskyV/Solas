using Solas.Components;

namespace Solas.Registries;

public interface ILogicRegistration : IRegistration;

public sealed class LogicAddingRegistry() : Registry(typeof(ILogicRegistration))
{
    private readonly Dictionary<string, Func<Entity, Logic>> _readers = [];

    public void Register<T>(string typeName) where T : Logic, new()
    {
        _readers[typeName] = entity => entity.AddLogic<T>();
    }

    internal Logic AddLogic(string typeName, Entity entity)
    {
        if (!_readers.TryGetValue(typeName, out var func))
            throw new InvalidOperationException(
                $"Type '{typeName}' is not registered for deserialization.");

        return func(entity);
    }
}