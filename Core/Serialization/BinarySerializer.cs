using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Solas.Serialization;

public static class BinarySerializer
{
    private static readonly ConcurrentDictionary<
        Type,
        Action<BinaryWriter, object>>
        _writers = new();

    private static readonly ConcurrentDictionary<
        Type,
        Func<BinaryReader, object>>
        _readers = new();

    public static void Write(
        BinaryWriter writer,
        object value)
    {
        writer.Write(value != null);

        if (value == null)
            return;
        
        switch (value)
        {
            case int v:
                writer.Write(v);
                return;

            case float v:
                writer.Write(v);
                return;

            case double v:
                writer.Write(v);
                return;

            case long v:
                writer.Write(v);
                return;

            case bool v:
                writer.Write(v);
                return;

            case string v:
                writer.Write(v);
                return;
        }
        var type = value.GetType();
        var action =
            _writers.GetOrAdd(
                type,
                CreateWriter);

        action(writer, value);
    }

    public static object Read(
        BinaryReader reader,
        Type type)
    {
        bool hasValue = reader.ReadBoolean();

        if (!hasValue)
            return null!;
        if (type == typeof(int))
            return reader.ReadInt32();

        if (type == typeof(float))
            return reader.ReadSingle();

        if (type == typeof(double))
            return reader.ReadDouble();

        if (type == typeof(long))
            return reader.ReadInt64();

        if (type == typeof(bool))
            return reader.ReadBoolean();

        if (type == typeof(string))
            return reader.ReadString();
        
        Func<BinaryReader, object> func =
            _readers.GetOrAdd(
                type,
                CreateReader);

        return func(reader);
    }

    private static Action<BinaryWriter, object>
        CreateWriter(Type type)
    {
        MethodInfo? method =
            type.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    if (m.Name != "Write")
                        return false;

                    ParameterInfo[] parameters =
                        m.GetParameters();

                    return
                        parameters.Length == 2 &&
                        parameters[0].ParameterType ==
                        typeof(BinaryWriter);
                });

        if (method == null)
            throw new InvalidOperationException(
                $"Type '{type}' does not contain static Write method.");

        ParameterExpression writerParameter =
            Expression.Parameter(
                typeof(BinaryWriter));

        ParameterExpression objectParameter =
            Expression.Parameter(
                typeof(object));

        UnaryExpression cast =
            Expression.Convert(
                objectParameter,
                type);

        MethodCallExpression call =
            Expression.Call(
                method,
                writerParameter,
                cast);

        return Expression
            .Lambda<Action<BinaryWriter, object>>(
                call,
                writerParameter,
                objectParameter)
            .Compile();
    }

    private static Func<BinaryReader, object>
        CreateReader(Type type)
    {
        MethodInfo? method =
            type.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    if (m.Name != "Read")
                        return false;

                    ParameterInfo[] parameters =
                        m.GetParameters();

                    return
                        parameters.Length == 1 &&
                        parameters[0].ParameterType ==
                        typeof(BinaryReader);
                });

        if (method == null)
            throw new InvalidOperationException(
                $"Type '{type}' does not contain static Read method.");

        ParameterExpression readerParameter =
            Expression.Parameter(
                typeof(BinaryReader));

        MethodCallExpression call =
            Expression.Call(
                method,
                readerParameter);

        UnaryExpression cast =
            Expression.Convert(
                call,
                typeof(object));

        return Expression
            .Lambda<Func<BinaryReader, object>>(
                cast,
                readerParameter)
            .Compile();
    }
}