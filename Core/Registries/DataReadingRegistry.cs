using Solas.Components;
using Solas.Generated;

namespace Solas.Registries;

public class DataReadingRegistry
{
    private readonly Dictionary<string, Func<FileStream, IData>> _readers = [];
    
    internal DataReadingRegistry()
    {
        DataReadingRegistration.Add(this);
    }

    public void Register<T>(string typeName) where T : IData
    {
        _readers[typeName] = stream => EngineContext.Serializer.Read<T>(stream);
    }

    internal IData Read(string typeName, FileStream stream)
    {
        if (!_readers.TryGetValue(typeName, out var func))
            throw new InvalidOperationException(
                $"Type '{typeName}' is not registered for binary deserialization.");

        return func(stream);
    }
}