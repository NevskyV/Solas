using Solas.Components;

namespace Solas.Registries;

public interface IDataRegistration : IRegistration;

public class DataSerializationRegistry() : Registry(typeof(IDataRegistration))
{
    private readonly Dictionary<Type, Action<IData, FileStream>> _writers = [];
    private readonly Dictionary<string, Func<FileStream, IData>> _readers = [];
    
    public void Register<T>(string typeName) where T : IData
    {
        _writers[typeof(T)] = (data, stream) => Query.Serializer.Write((T)data, stream);
        _readers[typeName] = stream => Query.Serializer.Read<T>(stream);
    }

    internal void Write(Type type, IData data, FileStream stream)
    {
        if (!_writers.TryGetValue(type, out var action))
            throw new InvalidOperationException(
                $"Type '{type}' is not registered for deserialization.");

        action(data, stream);
    }

    internal IData Read(string typeName, FileStream stream)
    {
        if (!_readers.TryGetValue(typeName, out var func))
            throw new InvalidOperationException(
                $"Type '{typeName}' is not registered for deserialization.");

        return func(stream);
    }
}