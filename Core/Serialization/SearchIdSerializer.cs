namespace Solas.Serialization;

internal static class SearchIdSerializer
{
    internal static void Write(string path, Guid objectId, uint offset)
    {
        using var stream = File.Open(path, FileMode.Append, FileAccess.Write);
        using var writer = new BinaryWriter(stream);
        
        writer.Write(objectId.ToByteArray());
        writer.Write(offset);
    }
    
    internal static void WriteAll(string path, IEnumerable<(Guid, uint)> objects)
    {
        using var stream = File.Open(path, FileMode.OpenOrCreate, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        foreach (var (objectId, offset) in objects)
        {
            writer.Write(objectId.ToByteArray());
            writer.Write(offset);
        }
    }
    
    internal static Dictionary<Guid, uint> ReadAll(string lookupPath)
    {
        using var stream = File.Open(lookupPath, FileMode.OpenOrCreate, FileAccess.Read);
        using var reader = new BinaryReader(stream);

        var result = new Dictionary<Guid, uint>();
        while (stream.Position < stream.Length)
        {
            var spaceId = new Guid(reader.ReadBytes(16));
            if (!result.ContainsKey(spaceId))
                result.Add(spaceId, reader.ReadUInt32());
            else
                result[spaceId] = reader.ReadUInt32();
        }

        return result;
    }
}