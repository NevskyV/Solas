using System.Reflection;

namespace Solas.Serialization.Core;

public abstract class Serializer
{
    private readonly Dictionary<Type, Delegate> _writers = [];
    private readonly Dictionary<Type, Delegate> _readers = [];

    protected Serializer(Type customSerializerType)
    {
        _writers.Add(typeof(byte), new Action<byte, FileStream>(Write));
        _writers.Add(typeof(byte[]), new Action<byte[], FileStream>(Write));
        _writers.Add(typeof(bool), new Action<bool, FileStream>(Write));
        _writers.Add(typeof(char), new Action<char, FileStream>(Write));
        _writers.Add(typeof(string), new Action<string, FileStream>(Write));
        _writers.Add(typeof(short), new Action<short, FileStream>(Write));
        _writers.Add(typeof(int), new Action<int, FileStream>(Write));
        _writers.Add(typeof(long), new Action<long, FileStream>(Write));
        _writers.Add(typeof(ushort), new Action<ushort, FileStream>(Write));
        _writers.Add(typeof(uint), new Action<uint, FileStream>(Write));
        _writers.Add(typeof(ulong), new Action<ulong, FileStream>(Write));
        _writers.Add(typeof(float), new Action<float, FileStream>(Write));
        _writers.Add(typeof(double), new Action<double, FileStream>(Write));
        _writers.Add(typeof(Guid), new Action<Guid, FileStream>(Write));
        
        _readers.Add(typeof(byte), ReadByte);
        _readers.Add(typeof(byte[]), ReadBytes);
        _readers.Add(typeof(bool), ReadBool);
        _readers.Add(typeof(char), ReadChar);
        _readers.Add(typeof(string), ReadString);
        _readers.Add(typeof(short), ReadInt16);
        _readers.Add(typeof(int), ReadInt32);
        _readers.Add(typeof(long), ReadInt64);
        _readers.Add(typeof(ushort), ReadUInt16);
        _readers.Add(typeof(uint), ReadUInt32);
        _readers.Add(typeof(ulong), ReadUInt64);
        _readers.Add(typeof(float), ReadFloat);
        _readers.Add(typeof(double), ReadDouble);
        _readers.Add(typeof(Guid), ReadGuid);

        GetAllCustomTypes(customSerializerType);
    }

    private void GetAllCustomTypes(Type customSerializerType)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            if (assembly.FullName != null &&
                (assembly.FullName.StartsWith("System") || assembly.FullName.StartsWith("Microsoft")))
                continue;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }

            var inheritors = types.Where(t =>
                t.IsClass && !t.IsAbstract && t.IsAssignableTo(customSerializerType));

            foreach (var type in inheritors)
            {
                var writeMethod = type.GetMethod("Write", BindingFlags.Public | BindingFlags.Instance);
                var readMethod = type.GetMethod("Read", BindingFlags.Public | BindingFlags.Instance);

                if (readMethod == null) continue;

                var instance = Activator.CreateInstance(type);
                var writeDelegate = Delegate.CreateDelegate(typeof(Action), instance, readMethod);
                var readDelegate = Delegate.CreateDelegate(typeof(Action), instance, readMethod);
                AddWriter(type, writeDelegate);
                AddReader(type, readDelegate);
            }
        }
    }
    
    public void AddWriter(Type type, Delegate writer) => _writers.Add(type, writer);
    public Delegate GetWriter(Type type) => _writers[type];
    
    public void AddReader(Type type, Delegate writer) => _readers.Add(type, writer);
    public Delegate GetReader(Type type) => _readers[type];
    
    public abstract void Write(byte value, FileStream stream);
    public abstract void Write(byte[] value, FileStream stream);
    public abstract void Write(bool value, FileStream stream);
    public abstract void Write(char value, FileStream stream);
    public abstract void Write(string value, FileStream stream);
    public abstract void Write(short value, FileStream stream);
    public abstract void Write(int value, FileStream stream);
    public abstract void Write(long value, FileStream stream);
    public abstract void Write(ushort value, FileStream stream);
    public abstract void Write(uint value, FileStream stream);
    public abstract void Write(ulong value, FileStream stream);
    public abstract void Write(float value, FileStream stream);
    public abstract void Write(double value, FileStream stream);
    public abstract void Write(Guid value, FileStream stream);
    
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