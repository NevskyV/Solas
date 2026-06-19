namespace Solas.SourceGenerators.Utils;

public record MemberMetadata(
    string Name,
    string TypeFullName,
    bool IsArray,
    string ElementTypeFullName,
    bool IsPrimitive,
    bool IsNullable);