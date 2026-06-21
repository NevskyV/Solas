using Solas.Assets;

namespace Solas.Registries;

public interface IAssetsRegistration : IRegistration;

public class AssetsSerializationRegistry() : Registry(typeof(IAssetsRegistration))
{
    private readonly Dictionary<Type, Action<Asset, FileStream>> _writers = [];
    private readonly Dictionary<string, Func<FileStream, Asset>> _readers = [];

    public void Register<T>(string typeName) where T : Asset
    {
        _writers[typeof(T)] = (asset, stream) => Query.Serializer.Write((T)asset, stream);
        _readers[typeName] = stream => Query.Serializer.Read<T>(stream);
    }

    internal void Write(Type type, Asset data, FileStream stream)
    {
        if (!_writers.TryGetValue(type, out var action))
            throw new InvalidOperationException(
                $"Type '{type}' is not registered for deserialization.");

        action(data, stream);
    }
    
    internal Asset Read(string typeName, FileStream stream)
    {
        if (!_readers.TryGetValue(typeName, out var func))
            throw new InvalidOperationException(
                $"Type '{typeName}' is not registered for deserialization.");

        return func(stream);
    }
}