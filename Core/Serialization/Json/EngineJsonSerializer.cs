using System.Globalization;
using System.Text.Json;
using Solas.Serialization.Core;

namespace Solas.Serialization.Json;

public class EngineJsonSerializer : Serializer
{
    private int _autoNameCounter;

    private Queue<object> _readQueue;
    private Utf8JsonWriter _writer;

    public override void Open(FileStream stream)
    {
        if (stream.CanWrite)
        {
            var options = new JsonWriterOptions { Indented = true };
            _writer = new Utf8JsonWriter(stream, options);
            _writer.WriteStartObject();
            _autoNameCounter = 0;
        }
        else if (stream.CanRead)
        {
            _readQueue = new Queue<object>();

            using (var doc = JsonDocument.Parse(stream))
            {
                FlattenJson(doc.RootElement, _readQueue);
            }
        }
    }

    public override void Close(FileStream stream)
    {
        if (_writer != null)
        {
            _writer.WriteEndObject();
            _writer.Flush();
            _writer.Dispose();
            _writer = null;
        }

        _readQueue = null;
    }

    private void FlattenJson(JsonElement element, Queue<object> queue)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                        FlattenJson(property.Value, queue);
                    else
                        queue.Enqueue(ExtractPrimitive(property.Value));

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) FlattenJson(item, queue);
                break;

            default:
                queue.Enqueue(ExtractPrimitive(element));
                break;
        }
    }

    private object ExtractPrimitive(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(), // Сохраняем как текст, парсить будем при чтении
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            _ => throw new InvalidOperationException("Неподдерживаемый тип токена")
        };
    }

    public override void BeginObject(FileStream stream, string name = null)
    {
        _writer!.WriteStartObject(GetPropertyName(name));
    }

    public override void EndObject(FileStream stream)
    {
        _writer!.WriteEndObject();
    }

    private string GetPropertyName(string name)
    {
        if (!string.IsNullOrEmpty(name)) return name;
        return $"Field_{_autoNameCounter++}";
    }

    private object GetNextValue()
    {
        if (_readQueue == null || _readQueue.Count == 0)
            throw new EndOfStreamException("Попытка считать данные за пределами JSON-потока.");
        return _readQueue.Dequeue();
    }


    public override void Write(byte value, FileStream stream, string name = null)
    {
        _writer!.WriteNumber(GetPropertyName(name), value);
    }

    public override void Write(bool value, FileStream stream, string name = null)
    {
        _writer!.WriteBoolean(GetPropertyName(name), value);
    }

    public override void Write(char value, FileStream stream, string name = null)
    {
        _writer!.WriteString(GetPropertyName(name), value.ToString());
    }

    public override void Write(string value, FileStream stream, string name = null)
    {
        _writer!.WriteString(GetPropertyName(name), value);
    }

    public override void Write(short value, FileStream stream, string name = null)
    {
        _writer!.WriteNumber(GetPropertyName(name), value);
    }

    public override void Write(int value, FileStream stream, string name = null)
    {
        _writer!.WriteNumber(GetPropertyName(name), value);
    }

    public override void Write(long value, FileStream stream, string name = null)
    {
        _writer!.WriteNumber(GetPropertyName(name), value);
    }

    public override void Write(ushort value, FileStream stream, string name = null)
    {
        _writer!.WriteNumber(GetPropertyName(name), value);
    }

    public override void Write(uint value, FileStream stream, string name = null)
    {
        _writer!.WriteNumber(GetPropertyName(name), value);
    }

    public override void Write(ulong value, FileStream stream, string name = null)
    {
        _writer!.WriteNumber(GetPropertyName(name), value);
    }

    public override void Write(float value, FileStream stream, string name = null)
    {
        _writer!.WriteNumber(GetPropertyName(name), value);
    }

    public override void Write(double value, FileStream stream, string name = null)
    {
        _writer!.WriteNumber(GetPropertyName(name), value);
    }

    public override void Write(Guid value, FileStream stream, string name = null)
    {
        _writer!.WriteString(GetPropertyName(name), value.ToString());
    }

    public override void Write(byte[] value, FileStream stream, string name = null)
    {
        _writer!.WriteString(GetPropertyName(name), Convert.ToBase64String(value));
    }

    public override string ReadString(FileStream stream)
    {
        return (string)GetNextValue();
    }

    public override bool ReadBool(FileStream stream)
    {
        return (bool)GetNextValue();
    }

    public override byte ReadByte(FileStream stream)
    {
        return byte.Parse((string)GetNextValue());
    }

    public override short ReadInt16(FileStream stream)
    {
        return short.Parse((string)GetNextValue());
    }

    public override int ReadInt32(FileStream stream)
    {
        return int.Parse((string)GetNextValue());
    }

    public override long ReadInt64(FileStream stream)
    {
        return long.Parse((string)GetNextValue());
    }

    public override ushort ReadUInt16(FileStream stream)
    {
        return ushort.Parse((string)GetNextValue());
    }

    public override uint ReadUInt32(FileStream stream)
    {
        return uint.Parse((string)GetNextValue());
    }

    public override ulong ReadUInt64(FileStream stream)
    {
        return ulong.Parse((string)GetNextValue());
    }

    public override float ReadFloat(FileStream stream)
    {
        return float.Parse((string)GetNextValue(), CultureInfo.InvariantCulture);
    }

    public override double ReadDouble(FileStream stream)
    {
        return double.Parse((string)GetNextValue(), CultureInfo.InvariantCulture);
    }

    public override Guid ReadGuid(FileStream stream)
    {
        return Guid.Parse((string)GetNextValue());
    }

    public override char ReadChar(FileStream stream)
    {
        var str = (string)GetNextValue();
        return string.IsNullOrEmpty(str) ? '\0' : str[0];
    }

    public override byte[] ReadBytes(int count, FileStream stream)
    {
        var base64 = (string)GetNextValue();
        return string.IsNullOrEmpty(base64) ? Array.Empty<byte>() : Convert.FromBase64String(base64);
    }
}