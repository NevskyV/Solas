namespace Solas.Registries;

public sealed class InjectSerializationRegistry
{
    private readonly Dictionary<Type, Action<FileStream>> _writers = [];
    private readonly Dictionary<string, Func<FileStream, (Guid, Guid)[]>> _readers = [];
    
    internal InjectSerializationRegistry()
    {
        //InjectSerializationRegistration.Add(this);
    }

    public void Register<T>(
        string typeName,
        Action<FileStream> write,
        Func<FileStream, (Guid, Guid)[]> read)
    {
        _writers[typeof(T)] = write;
        _readers[typeName] = read;
    }

    internal void Write(object data, FileStream stream)
    {
        if (!_writers.TryGetValue(data.GetType(), out var action))
            throw new InvalidOperationException(
                $"Type '{data.GetType()}' is not registered for binary serialization.");

        action(stream);
    }

    internal (Guid, Guid)[] Read(string typeName, FileStream stream)
    {
        if (!_readers.TryGetValue(typeName, out var func))
            throw new InvalidOperationException(
                $"Type '{typeName}' is not registered for binary deserialization.");
        
        return func(stream);
    }
}