using System.Text.Json;
using System.Text.Json.Serialization;
using Solas.Serialization.Core;

namespace Solas.Serialization.Json;

public class EngineJsonSerializer() : Serializer(typeof(CustomJsonSerializer<>))
{
    private static readonly JsonSerializerOptions _options = new ()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public override void Write(byte value, FileStream stream)
    {
        JsonSerializer.Serialize(stream, value, _options);
    }

    public override void Write(byte[] value, FileStream stream)
    {
        JsonSerializer.Serialize(stream, value, _options);
    }

    public override void Write(bool value, FileStream stream)
    {
        JsonSerializer.Serialize(stream, value, _options);
    }

    public override void Write(char value, FileStream stream)
    {
        JsonSerializer.Serialize(stream, value, _options);
    }

    public override void Write(string value, FileStream stream)
    {
        JsonSerializer.Serialize(stream, value, _options);
    }

    public override void Write(short value, FileStream stream)
    {
        JsonSerializer.Serialize(stream, value, _options);
    }

    public override void Write(int value, FileStream stream)
    {
        JsonSerializer.Serialize(stream, value, _options);
    }

    public override void Write(long value, FileStream stream)
    {
        JsonSerializer.Serialize(stream, value, _options);
    }

    public override void Write(ushort value, FileStream stream)
    {
        JsonSerializer.Serialize(stream, value, _options);
    }

    public override void Write(uint value, FileStream stream)
    {
        JsonSerializer.Serialize(stream, value, _options);
    }

    public override void Write(ulong value, FileStream stream)
    {
        JsonSerializer.Serialize(stream, value, _options);
    }

    public override void Write(float value, FileStream stream)
    {
        JsonSerializer.Serialize(stream, value, _options);
    }

    public override void Write(double value, FileStream stream)
    {
        JsonSerializer.Serialize(stream, value, _options);
    }

    public override void Write(Guid value, FileStream stream)
    {
        JsonSerializer.Serialize(stream, value, _options);
    }

    public override byte ReadByte(FileStream stream)
    {
        return JsonSerializer.Deserialize<byte>(stream, _options);
    }

    public override byte[] ReadBytes(int count, FileStream stream)
    {
        return JsonSerializer.Deserialize<byte[]>(stream, _options);
    }

    public override bool ReadBool(FileStream stream)
    {
        return JsonSerializer.Deserialize<bool>(stream, _options);
    }

    public override char ReadChar(FileStream stream)
    {
        return JsonSerializer.Deserialize<char>(stream, _options);
    }

    public override string ReadString(FileStream stream)
    {
        return JsonSerializer.Deserialize<string>(stream, _options);
    }

    public override short ReadInt16(FileStream stream)
    {
        return JsonSerializer.Deserialize<short>(stream, _options);
    }

    public override int ReadInt32(FileStream stream)
    {
        return JsonSerializer.Deserialize<int>(stream, _options);
    }

    public override long ReadInt64(FileStream stream)
    {
        return JsonSerializer.Deserialize<long>(stream, _options);
    }

    public override ushort ReadUInt16(FileStream stream)
    {
        return JsonSerializer.Deserialize<ushort>(stream, _options);
    }

    public override uint ReadUInt32(FileStream stream)
    {
        return JsonSerializer.Deserialize<uint>(stream, _options);
    }

    public override ulong ReadUInt64(FileStream stream)
    {
        return JsonSerializer.Deserialize<ulong>(stream, _options);
    }

    public override float ReadFloat(FileStream stream)
    {
        return JsonSerializer.Deserialize<float>(stream, _options);
    }

    public override double ReadDouble(FileStream stream)
    {
        return JsonSerializer.Deserialize<double>(stream, _options);
    }

    public override Guid ReadGuid(FileStream stream)
    {
        return JsonSerializer.Deserialize<Guid>(stream, _options);
    }
}