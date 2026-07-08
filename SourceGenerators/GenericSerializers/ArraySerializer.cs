using Solas.SourceGenerators.Utils;

namespace Solas.SourceGenerators.GenericSerializers;

public class ArraySerializer : IGenericSerializer
{
    public string Write(MemberMetadata member, string accessPath)
    {
        return $"serializer.WriteArray(value.{accessPath}, stream, serializer.Write, \"{member.Name}\");";
    }

    public string Read(MemberMetadata member)
    {
        return member.IsPrimitive
            ? $"Query.Serializer.ReadArray(stream, Query.Serializer.Read{SerializationGenerator.GetPrimitiveMethodSuffix(member.ElementTypeFullName)})"
            : $"Query.Serializer.ReadArray<{member.ElementTypeFullName}>(stream)";
    }
}