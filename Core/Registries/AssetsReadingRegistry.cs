using Solas.Assets;
using Solas.Generated;

namespace Solas.Registries;

public class AssetsReadingRegistry
{
    private readonly Dictionary<string, Func<FileStream, Asset>> _readers = [];

    internal AssetsReadingRegistry()
    {
        AssetsReadingRegistration.Add(this);
    }

    public void Register<T>(string typeName) where T : Asset
    {
        _readers[typeName] = stream => Query.Serializer.Read<T>(stream);
    }

    internal Asset Read(string typeName, FileStream stream)
    {
        if (!_readers.TryGetValue(typeName, out var func))
            throw new InvalidOperationException(
                $"Type '{typeName}' is not registered for binary deserialization.");

        return func(stream);
    }
}