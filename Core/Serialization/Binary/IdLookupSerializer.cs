namespace Solas.Serialization.Binary;

public static class IdLookupSerializer
{
    public static void Write(BinaryWriter writer, Guid objectId, uint offset)
    {
        writer.Write(objectId.ToByteArray());
        writer.Write(offset);
    }

    public static Dictionary<Guid, uint> ReadAll(string lookupPath)
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