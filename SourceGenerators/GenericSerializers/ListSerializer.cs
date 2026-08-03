using Solas.SourceGenerators.Utils;

namespace Solas.SourceGenerators.GenericSerializers;

public class ListSerializer : IGenericSerializer
{
    public string Write(MemberMetadata member, string accessPath)
    {
        if (member.IsEnum)
        {
            return $"serializer.WriteArray(value.{accessPath}.ToArray(), stream, (v, s, n) => serializer.Write((int)v, s, n), \"{member.Name}\");";
        }
        return $"serializer.WriteArray(value.{accessPath}.ToArray(), stream, serializer.Write, \"{member.Name}\");";
    }

    public string Read(MemberMetadata member)
    {
        if (member.IsEnum)
        {
            return $"Query.Serializer.ReadArray(stream, s => ({member.ElementTypeFullName})Query.Serializer.ReadInt32(s)).ToList();";
        }
        return member.IsPrimitive
            ? $"Query.Serializer.ReadArray(stream, Query.Serializer.Read{SerializationGenerator.GetPrimitiveMethodSuffix(member.ElementTypeFullName)}).ToList();"
            : $"Query.Serializer.ReadArray<{member.ElementTypeFullName}>(stream).ToList();";
    }
}