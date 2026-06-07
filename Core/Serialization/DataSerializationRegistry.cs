using Solas.Components;

namespace Solas.Serialization;

public static class DataSerializationRegistry
{
    private static readonly Dictionary<Type, Action<BinaryWriter, IData, Entity>> _writers = [];
    private static readonly Dictionary<string, Func<BinaryReader, (IData data, (Guid, Guid)[] guids)>> _readers = [];

    public static void Register<T>(
        string typeName,
        Action<BinaryWriter, T, Entity> write,
        Func<BinaryReader, (T data, (Guid, Guid)[] guids)> read) where T : IData
    {
        _writers[typeof(T)] = (writer, data, entity) => write(writer, (T)data, entity);
        _readers[typeName] = reader =>
        {
            var (data, guids) = read(reader);
            return (data, guids);
        };
    }

    public static void Write(BinaryWriter writer, IData data, Entity entity)
    {
        if (!_writers.TryGetValue(data.GetType(), out var action))
            throw new InvalidOperationException(
                $"Type '{data.GetType()}' is not registered for binary serialization.");

        action(writer, data, entity);
    }

    public static IData Read(string typeName, BinaryReader reader, out (Guid, Guid)[] guids)
    {
        if (!_readers.TryGetValue(typeName, out var factory))
            throw new InvalidOperationException(
                $"Type '{typeName}' is not registered for binary deserialization.");

        var (data, readGuids) = factory(reader);
        guids = readGuids;
        return data;
    }
}