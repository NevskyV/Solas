using Solas.Registries;

namespace Solas.Serialization.Core;

public interface ISerializeRegistration : IRegistration;

public abstract class Serializer() : Registry(typeof(ISerializeRegistration))
{
    private readonly Dictionary<Type, object> _serializers = [];

    public void AddSerializer<T>(ICustomSerializer<T> serializer)
    {
        _serializers.Add(typeof(T), serializer);
    }

    private ICustomSerializer<T> GetSerializer<T>()
    {
        return (ICustomSerializer<T>)_serializers[typeof(T)];
    }

    //Decorators
    public virtual void Open(FileStream stream) { }
    public virtual void Close(FileStream stream){ }
    public virtual void BeginObject(FileStream stream, string name = null) { }
    public virtual void EndObject(FileStream stream) { }

    public void Write<T>(T value, FileStream stream, string name = null)
    {
        GetSerializer<T>().Write(value, stream, name);
    }

    public virtual void WriteArray<T>(T[] value, FileStream stream, Action<T, FileStream, string> action = null,
        string name = null)
    {
        Write(value.Length, stream, "ArrayLenght");
        action ??= Write;
        foreach (var item in value)
            action(item, stream, null);
    }

    public abstract void Write(byte value, FileStream stream, string name = null);
    public abstract void Write(byte[] value, FileStream stream, string name = null);
    public abstract void Write(bool value, FileStream stream, string name = null);
    public abstract void Write(char value, FileStream stream, string name = null);
    public abstract void Write(string value, FileStream stream, string name = null);
    public abstract void Write(short value, FileStream stream, string name = null);
    public abstract void Write(int value, FileStream stream, string name = null);
    public abstract void Write(long value, FileStream stream, string name = null);
    public abstract void Write(ushort value, FileStream stream, string name = null);
    public abstract void Write(uint value, FileStream stream, string name = null);
    public abstract void Write(ulong value, FileStream stream, string name = null);
    public abstract void Write(float value, FileStream stream, string name = null);
    public abstract void Write(double value, FileStream stream, string name = null);
    public abstract void Write(Guid value, FileStream stream, string name = null);

    public T Read<T>(FileStream stream)
    {
        return GetSerializer<T>().Read(stream);
    }

    public T[] ReadArray<T>(FileStream stream, Func<FileStream, T> func = null)
    {
        var length = ReadInt32(stream);
        var result = new T[length];
        func ??= Read<T>;
        for (var i = 0; i < length; i++)
            result[i] = func(stream);

        return result;
    }

    public abstract byte ReadByte(FileStream stream);
    public abstract byte[] ReadBytes(int count, FileStream stream);
    public abstract bool ReadBool(FileStream stream);
    public abstract char ReadChar(FileStream stream);
    public abstract string ReadString(FileStream stream);
    public abstract short ReadInt16(FileStream stream);
    public abstract int ReadInt32(FileStream stream);
    public abstract long ReadInt64(FileStream stream);
    public abstract ushort ReadUInt16(FileStream stream);
    public abstract uint ReadUInt32(FileStream stream);
    public abstract ulong ReadUInt64(FileStream stream);
    public abstract float ReadFloat(FileStream stream);
    public abstract double ReadDouble(FileStream stream);
    public abstract Guid ReadGuid(FileStream stream);
}