namespace Solas.SourceGenerators.Utils;

public record TypeMetadata(
    string Name,
    string FullName,
    string Namespace,
    string AssemblyName,
    bool IsStruct,
    bool IsValueType);