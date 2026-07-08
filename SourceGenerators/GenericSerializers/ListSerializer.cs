using Solas.SourceGenerators.Utils;

namespace Solas.SourceGenerators.GenericSerializers;

public class ListSerializer : IGenericSerializer
{
    public string Write(MemberMetadata member, string accessPath)
    {
        return $"serializer.WriteArray(value.{accessPath}.ToArray(), stream, serializer.Write, \"{member.Name}\");";
    }

    public string Read(MemberMetadata member)
    {
        return member.IsPrimitive
            ? $"Query.Serializer.ReadArray(stream, Query.Serializer.Read{SerializationGenerator.GetPrimitiveMethodSuffix(member.ElementTypeFullName)}).ToList();"
            : $"Query.Serializer.ReadArray<{member.ElementTypeFullName}>(stream).ToList();";
    }
}