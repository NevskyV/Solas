using Solas.SourceGenerators.GenericSerializers;

namespace Solas.SourceGenerators.Utils;

public struct MemberMetadata
{
    public string Name;
    public string TypeFullName;
    public IGenericSerializer? GenericSerializer;
    public string ElementTypeFullName;
    public bool IsPrimitive;
    public bool IsNullable;
    public bool IsValueType;
    public bool IsReferenceLink;
}