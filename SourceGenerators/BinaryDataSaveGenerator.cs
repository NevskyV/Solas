using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Solas.SourceGenerators;

[Generator]
public sealed class BinarySerializerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<TypeDeclarationSyntax> types =
            context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (node, _) => node is TypeDeclarationSyntax,
                    static (ctx, _) => (TypeDeclarationSyntax)ctx.Node);

        IncrementalValueProvider<
            (Compilation, ImmutableArray<TypeDeclarationSyntax>)>
            compilationAndTypes =
                context.CompilationProvider.Combine(types.Collect());

        context.RegisterSourceOutput(
            compilationAndTypes,
            static (ctx, source) =>
            {
                (Compilation compilation, ImmutableArray<TypeDeclarationSyntax> syntaxes) = source;

                INamedTypeSymbol? dataInterface =
                    compilation.GetTypeByMetadataName("Solas.Components.IData");

                if (dataInterface == null)
                    return;

                INamedTypeSymbol? referenceableInterface =
                    compilation.GetTypeByMetadataName("Solas.Interfaces.IReferenceable");

                INamedTypeSymbol? entityType =
                    compilation.GetTypeByMetadataName("Solas.Components.Entity");

                INamedTypeSymbol? logicType =
                    compilation.GetTypeByMetadataName("Solas.Components.Logic");

                HashSet<INamedTypeSymbol> dataTypes =
                    new(SymbolEqualityComparer.Default);

                foreach (TypeDeclarationSyntax syntax in syntaxes)
                {
                    SemanticModel semantic =
                        compilation.GetSemanticModel(syntax.SyntaxTree);

                    if (semantic.GetDeclaredSymbol(syntax) is not INamedTypeSymbol symbol)
                        continue;

                    if (!symbol.AllInterfaces.Any(i =>
                            SymbolEqualityComparer.Default.Equals(i, dataInterface)))
                        continue;

                    dataTypes.Add(symbol);
                }

                var context = new GenerationContext(
                    compilation,
                    referenceableInterface,
                    entityType,
                    logicType,
                    dataInterface);

                foreach (INamedTypeSymbol type in dataTypes)
                {
                    ctx.AddSource(
                        $"{type.Name}.Binary.g.cs",
                        SourceText.From(
                            GenerateSerializer(type, context, compilation.AssemblyName),
                            Encoding.UTF8));
                }
            });
    }

    private sealed class GenerationContext(
        Compilation compilation,
        INamedTypeSymbol? referenceableInterface,
        INamedTypeSymbol? entityType,
        INamedTypeSymbol? logicType,
        INamedTypeSymbol? dataInterface)
    {
        public Compilation Compilation { get; } = compilation;
        public INamedTypeSymbol? ReferenceableInterface { get; } = referenceableInterface;
        public INamedTypeSymbol? EntityType { get; } = entityType;
        public INamedTypeSymbol? LogicType { get; } = logicType;
        public INamedTypeSymbol? DataInterface { get; } = dataInterface;
    }

    private sealed class InjectableField
    {
        public required string Name { get; init; }
        public required ITypeSymbol Type { get; init; }
        public required bool IsDataProperty { get; init; }
    }

    private static string GenerateSerializer(
        INamedTypeSymbol type,
        GenerationContext context,
        string? assemblyName)
    {
        var ns = type.ContainingNamespace.ToDisplayString();
        var name = type.Name;

        var keyword = type.TypeKind switch
        {
            TypeKind.Struct => "struct",
            _ => "class"
        };

        var typeParameters = type.TypeParameters.Length == 0
            ? ""
            : $"<{string.Join(", ", type.TypeParameters.Select(p => p.Name))}>";

        var constraints = string.Join(
            "\n",
            type.TypeParameters.Select(p =>
            {
                List<string> parts = [];

                if (p.HasReferenceTypeConstraint)
                    parts.Add("class");

                if (p.HasValueTypeConstraint)
                    parts.Add("struct");

                foreach (var constraint in p.ConstraintTypes)
                    parts.Add(constraint.ToDisplayString());

                if (p.HasConstructorConstraint)
                    parts.Add("new()");

                return parts.Count > 0
                    ? $"where {p.Name} : {string.Join(", ", parts)}"
                    : "";
            }));

        List<InjectableField> injectableFields = [];
        StringBuilder write = new();
        StringBuilder read = new();
        StringBuilder readGuids = new();

        foreach (var field in GetDeclaredMembers(type).OfType<IFieldSymbol>()
                     .Where(static f =>
                         !f.IsStatic &&
                         !f.IsImplicitlyDeclared &&
                         f.Type.TypeKind != TypeKind.Delegate &&
                         f.DeclaredAccessibility == Accessibility.Public))
        {
            AppendMemberSerialization(
                field.Name,
                $"value.{field.Name}",
                $"result.{field.Name}",
                field.Type,
                write,
                read,
                readGuids,
                injectableFields,
                context);
        }

        foreach (var property in GetDeclaredMembers(type).OfType<IPropertySymbol>()
                     .Where(static p =>
                         !p.IsStatic &&
                         !p.IsImplicitlyDeclared &&
                         p.Type.TypeKind != TypeKind.Delegate &&
                         p.GetMethod != null &&
                         p.SetMethod != null &&
                         p.DeclaredAccessibility == Accessibility.Public))
        {
            AppendMemberSerialization(
                property.Name,
                $"value.{property.Name}",
                $"result.{property.Name}",
                property.Type,
                write,
                read,
                readGuids,
                injectableFields,
                context);
        }

        StringBuilder inject = new();
        var injectIndex = 0;

        foreach (var field in injectableFields)
        {
            inject.AppendLine(GenerateInjectAssignment(field, injectIndex++, context));
        }

        var injectMethod = injectableFields.Count == 0
            ? ""
            : $@"
    public void Inject((Guid, Guid)[] guids)
    {{
{inject}    }}";

        var typeKey = GetTypeKey(type, assemblyName);
        var isStruct = type.TypeKind == TypeKind.Struct;
        var binaryClassName = $"{name}Binary";

        var serializationMethods = $@"
    public const int BinaryVersion = 1;

    public static void Write(
        BinaryWriter writer,
        {name}{typeParameters} value,
        Entity entity = null)
    {{
        writer.Write(BinaryVersion);
{write}    }}

    public static ({name}{typeParameters} data, (Guid, Guid)[] guids) ReadInternal(BinaryReader reader)
    {{
        _ = reader.ReadInt32();

        {name}{typeParameters} result = new();
        var guids = new List<(Guid, Guid)>();

{read}
{readGuids}
        return (result, guids.ToArray());
    }}";

        var readMethod = isStruct
            ? $@"
    public static {name}{typeParameters} Read(BinaryReader reader) =>
        ReadInternal(reader).data;"
            : $@"
    public static {name}{typeParameters} Read(BinaryReader reader)
    {{
        var (data, guids) = ReadInternal(reader);
        data._serializationGuids = guids;
        return data;
    }}";

        if (isStruct)
        {
            return $@"
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Solas.Components;
using Solas.Interfaces;
using Solas.Serialization;

namespace {ns};

internal static class {binaryClassName}
{{
    [ModuleInitializer]
    internal static void RegisterBinarySerialization()
    {{
        DataSerializationRegistry.Register<{name}{typeParameters}>(
            ""{typeKey}"",
            Write,
            ReadInternal);
    }}
{serializationMethods}{readMethod}
}}

public partial {keyword} {name}{typeParameters}{constraints}
{{
    public (Guid, Guid)[] SerializationGuids => [];

    public void Write(BinaryWriter writer, Entity entity) =>
        {binaryClassName}.Write(writer, this, entity);

    public static void Write(
        BinaryWriter writer,
        {name}{typeParameters} value,
        Entity entity = null) =>
        {binaryClassName}.Write(writer, value, entity);
{injectMethod}
}}";
        }

        return $@"
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Solas.Components;
using Solas.Interfaces;
using Solas.Serialization;

namespace {ns};

public partial {keyword} {name}{typeParameters}{constraints}
{{
    [ModuleInitializer]
    internal static void RegisterBinarySerialization()
    {{
        DataSerializationRegistry.Register<{name}{typeParameters}>(
            ""{typeKey}"",
            Write,
            ReadInternal);
    }}

    private (Guid, Guid)[] _serializationGuids = [];

    public (Guid, Guid)[] SerializationGuids => _serializationGuids;

    public void Write(BinaryWriter writer, Entity entity) =>
        Write(writer, this, entity);
{serializationMethods}{readMethod}
{injectMethod}
}}";
    }

    private static void AppendMemberSerialization(
        string name,
        string writeAccess,
        string readAccess,
        ITypeSymbol memberType,
        StringBuilder write,
        StringBuilder read,
        StringBuilder readGuids,
        List<InjectableField> injectableFields,
        GenerationContext context)
    {
        if (TryUnwrapDataProperty(memberType, out ITypeSymbol? innerType))
        {
            if (IsReferenceField(innerType!, context))
            {
                injectableFields.Add(new InjectableField
                {
                    Name = name,
                    Type = innerType!,
                    IsDataProperty = true
                });
            }

            write.AppendLine(
                GenerateDataPropertyWrite(writeAccess, innerType!, 2, context, injectableFields));
            read.AppendLine(
                GenerateDataPropertyRead(
                    readAccess,
                    innerType!,
                    2,
                    context,
                    injectableFields,
                    readGuids));
            return;
        }

        write.AppendLine(GenerateWrite(writeAccess, memberType, 2, context, injectableFields));
        read.AppendLine(GenerateRead(readAccess, memberType, 2, context, injectableFields, readGuids));
    }

    private static string GenerateDataPropertyWrite(
        string writeAccess,
        ITypeSymbol innerType,
        int indent,
        GenerationContext context,
        List<InjectableField> injectableFields)
    {
        string valueAccess = $"{writeAccess}.Value";
        string valueBody = IsReferenceField(innerType, context)
            ? GenerateReferenceWrite(valueAccess, innerType, indent + 1, context)
            : GenerateWrite(valueAccess, innerType, indent + 1, context, injectableFields);

        string pad = Pad(indent);
        return $@"{pad}writer.Write({writeAccess} != null);
{pad}if ({writeAccess} != null)
{pad}{{
{valueBody}
{pad}}}";
    }

    private static string GenerateDataPropertyRead(
        string readAccess,
        ITypeSymbol innerType,
        int indent,
        GenerationContext context,
        List<InjectableField> injectableFields,
        StringBuilder readGuids)
    {
        string valueAccess = $"{readAccess}.Value";
        string valueBody = IsReferenceField(innerType, context)
            ? GenerateReferenceRead(readGuids, innerType, indent + 1)
            : GenerateRead(valueAccess, innerType, indent + 1, context, injectableFields, readGuids);

        string pad = Pad(indent);
        return $@"{pad}if (reader.ReadBoolean())
{pad}{{
{Pad(indent + 1)}{readAccess} ??= new Solas.ComponentUtils.DataProperty<{innerType.ToDisplayString()}>();
{valueBody}
{pad}}}";
    }

    private static string GenerateInjectAssignment(
        InjectableField field,
        int index,
        GenerationContext context)
    {
        string access = field.IsDataProperty
            ? $"{field.Name}.Value"
            : field.Name;

        if (IsEntity(field.Type, context))
        {
            return
                $"        {access} = Solas.Command.Inject(guids[{index}].Item1, guids[{index}].Item2);";
        }

        if (IsLogic(field.Type, context))
        {
            return
                $"        {access} = Solas.Command.Inject(guids[{index}].Item1, guids[{index}].Item2).GetLogic<{field.Type.ToDisplayString()}>();";
        }

        if (IsData(field.Type, context))
        {
            return
                $"        {access} = Solas.Command.Inject(guids[{index}].Item1, guids[{index}].Item2).GetData<{field.Type.ToDisplayString()}>();";
        }

        return
            $"        {access} = Solas.Command.Inject<{field.Type.ToDisplayString()}>(guids[{index}].Item1, guids[{index}].Item2);";
    }

    private static string GenerateReferenceWrite(
        string access,
        ITypeSymbol type,
        int indent,
        GenerationContext context,
        string entityParameter = "entity")
    {
        string[] statements = IsLogic(type, context)
            ?
            [
                $"writer.Write({access}.Entity.Id.ToByteArray());",
                $"writer.Write({access}.Entity.GetSpaceId().ToByteArray());"
            ]
            : IsData(type, context)
                ?
                [
                    $"var owner = Solas.Query.TryGetEntityFor({access}, {entityParameter}?.CurrentSpace);",
                    "writer.Write(owner.Id.ToByteArray());",
                    "writer.Write(owner.GetSpaceId().ToByteArray());"
                ]
                :
                [
                    $"writer.Write({access}.Id.ToByteArray());",
                    $"writer.Write({access}.GetSpaceId().ToByteArray());"
                ];

        if (!CanBeNull(type))
            return IndentLines(statements, indent);

        string pad = Pad(indent);
        string inner = IndentLines(statements, indent + 1);
        return $@"{pad}writer.Write({access} != null);
{pad}if ({access} != null)
{pad}{{
{inner}
{pad}}}";
    }

    private static string GenerateReferenceRead(
        StringBuilder readGuids,
        ITypeSymbol type,
        int indent)
    {
        const string readGuidsStatement =
            "guids.Add((new Guid(reader.ReadBytes(16)), new Guid(reader.ReadBytes(16))));";

        if (!CanBeNull(type))
            return IndentLines([readGuidsStatement], indent);

        string pad = Pad(indent);
        return $@"{pad}if (reader.ReadBoolean())
{pad}{{
{IndentLines([readGuidsStatement], indent + 1)}
{pad}}}";
    }

    private static string GenerateWrite(
        string access,
        ITypeSymbol type,
        int indent,
        GenerationContext context,
        List<InjectableField> injectableFields)
    {
        string pad = Pad(indent);

        if (IsNullable(type, out ITypeSymbol? innerNullable))
        {
            string inner = GenerateWrite(
                $"{access}.Value",
                innerNullable!,
                indent + 1,
                context,
                injectableFields);

            return $@"{pad}writer.Write({access} != null);
{pad}if ({access} != null)
{pad}{{
{inner}
{pad}}}";
        }

        if (IsReferenceField(type, context))
        {
            return GenerateReferenceWrite(access, type, indent, context);
        }

        if (type.ToDisplayString() == "System.Guid")
        {
            return $"{pad}writer.Write({access}.ToByteArray());";
        }

        if (type.ToDisplayString() == "System.Numerics.Vector3")
        {
            return IndentLines(
                [
                    $"writer.Write({access}.X);",
                    $"writer.Write({access}.Y);",
                    $"writer.Write({access}.Z);"
                ],
                indent);
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            return $"{pad}writer.Write((int){access});";
        }

        if (type is ITypeParameterSymbol)
        {
            return $"{pad}BinarySerializer.Write(writer, {access});";
        }

        if (type.SpecialType is not SpecialType.System_String && type is IArrayTypeSymbol array)
        {
            string loopBody = GenerateWrite(
                "item",
                array.ElementType,
                indent + 2,
                context,
                injectableFields);

            return $@"{pad}writer.Write({access}.Length);
{pad}foreach (var item in {access})
{pad}{{
{loopBody}
{pad}}}";
        }

        if (type.SpecialType is not SpecialType.System_String &&
            IsEnumerable(type, out ITypeSymbol? itemType))
        {
            string loopBody = GenerateWrite(
                "item",
                itemType!,
                indent + 2,
                context,
                injectableFields);

            return $@"{pad}writer.Write({access}.Count());
{pad}foreach (var item in {access})
{pad}{{
{loopBody}
{pad}}}";
        }

        return type.SpecialType switch
        {
            SpecialType.System_Int32 or
            SpecialType.System_Single or
            SpecialType.System_Double or
            SpecialType.System_Int64 or
            SpecialType.System_UInt32 or
            SpecialType.System_UInt64 or
            SpecialType.System_Int16 or
            SpecialType.System_UInt16 or
            SpecialType.System_Byte or
            SpecialType.System_SByte or
            SpecialType.System_Char or
            SpecialType.System_Boolean or
            SpecialType.System_String
                => $"{pad}writer.Write({access});",

            _ => WrapNullableWrite(
                access,
                type,
                $"{type.ToDisplayString()}.Write(writer, {access});",
                indent)
        };
    }

    private static string WrapNullableWrite(
        string access,
        ITypeSymbol type,
        string statement,
        int indent)
    {
        if (!CanBeNull(type))
            return IndentLines([statement], indent);

        string pad = Pad(indent);
        string inner = IndentLines([statement], indent + 1);
        return $@"{pad}writer.Write({access} != null);
{pad}if ({access} != null)
{pad}{{
{inner}
{pad}}}";
    }

    private static string WrapNullableRead(
        string access,
        ITypeSymbol type,
        string statement,
        int indent)
    {
        if (!CanBeNull(type))
            return IndentLines([statement], indent);

        string pad = Pad(indent);
        string inner = IndentLines([statement], indent + 1);
        return $@"{pad}if (reader.ReadBoolean())
{pad}{{
{inner}
{pad}}}";
    }

    private static string GenerateRead(
        string access,
        ITypeSymbol type,
        int indent,
        GenerationContext context,
        List<InjectableField> injectableFields,
        StringBuilder readGuids)
    {
        string pad = Pad(indent);

        if (IsNullable(type, out ITypeSymbol? innerNullable))
        {
            string inner = GenerateRead(
                $"{access}.Value",
                innerNullable!,
                indent + 1,
                context,
                injectableFields,
                readGuids);

            return $@"{pad}if (reader.ReadBoolean())
{pad}{{
{inner}
{pad}}}";
        }

        if (IsReferenceField(type, context))
        {
            injectableFields.Add(new InjectableField
            {
                Name = ToInjectMemberName(access),
                Type = type,
                IsDataProperty = false
            });

            return GenerateReferenceRead(readGuids, type, indent);
        }

        if (type.ToDisplayString() == "System.Guid")
        {
            return $"{pad}{access} = new Guid(reader.ReadBytes(16));";
        }

        if (type.ToDisplayString() == "System.Numerics.Vector3")
        {
            return IndentLines(
                [
                    $"{access} = new System.Numerics.Vector3(",
                    "    reader.ReadSingle(),",
                    "    reader.ReadSingle(),",
                    "    reader.ReadSingle());"
                ],
                indent);
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            return $"{pad}{access} = ({type.ToDisplayString()})reader.ReadInt32();";
        }

        if (type is ITypeParameterSymbol)
        {
            return
                $"{pad}{access} = ({type.ToDisplayString()})" +
                $"BinarySerializer.Read(reader, typeof({type.ToDisplayString()}));";
        }

        if (type.SpecialType is not SpecialType.System_String && type is IArrayTypeSymbol array)
        {
            string element = array.ElementType.ToDisplayString();
            string loopBody = GenerateRead(
                $"{access}[i]",
                array.ElementType,
                indent + 2,
                context,
                injectableFields,
                readGuids);

            return $@"{pad}int length = reader.ReadInt32();
{pad}{access} = new {element}[length];
{pad}for (int i = 0; i < length; i++)
{pad}{{
{loopBody}
{pad}}}";
        }

        if (type.SpecialType is not SpecialType.System_String &&
            IsEnumerable(type, out ITypeSymbol? itemType))
        {
            string collection = type.ToDisplayString();
            string item = itemType!.ToDisplayString();
            string loopBody = GenerateRead(
                "value",
                itemType,
                indent + 2,
                context,
                injectableFields,
                readGuids);

            return $@"{pad}int count = reader.ReadInt32();
{pad}{access} = new {collection}();
{pad}for (int i = 0; i < count; i++)
{pad}{{
{Pad(indent + 1)}{item} value = default!;
{loopBody}
{Pad(indent + 1)}{access}.Add(value);
{pad}}}";
        }

        return type.SpecialType switch
        {
            SpecialType.System_Int32 => $"{pad}{access} = reader.ReadInt32();",
            SpecialType.System_Single => $"{pad}{access} = reader.ReadSingle();",
            SpecialType.System_Double => $"{pad}{access} = reader.ReadDouble();",
            SpecialType.System_Int64 => $"{pad}{access} = reader.ReadInt64();",
            SpecialType.System_UInt32 => $"{pad}{access} = reader.ReadUInt32();",
            SpecialType.System_UInt64 => $"{pad}{access} = reader.ReadUInt64();",
            SpecialType.System_Int16 => $"{pad}{access} = reader.ReadInt16();",
            SpecialType.System_UInt16 => $"{pad}{access} = reader.ReadUInt16();",
            SpecialType.System_Byte => $"{pad}{access} = reader.ReadByte();",
            SpecialType.System_SByte => $"{pad}{access} = reader.ReadSByte();",
            SpecialType.System_Char => $"{pad}{access} = reader.ReadChar();",
            SpecialType.System_Boolean => $"{pad}{access} = reader.ReadBoolean();",
            SpecialType.System_String => $"{pad}{access} = reader.ReadString();",
            _ => WrapNullableRead(
                access,
                type,
                $"{access} = {type.ToDisplayString()}.Read(reader);",
                indent)
        };
    }

    private static bool IsReferenceField(ITypeSymbol type, GenerationContext context)
    {
        if (IsEntity(type, context) || IsLogic(type, context) || IsData(type, context))
            return true;

        if (context.ReferenceableInterface == null)
            return false;

        return type.AllInterfaces.Any(i =>
                   SymbolEqualityComparer.Default.Equals(i, context.ReferenceableInterface)) ||
               SymbolEqualityComparer.Default.Equals(type, context.ReferenceableInterface);
    }

    private static bool IsEntity(ITypeSymbol type, GenerationContext context) =>
        context.EntityType != null &&
        SymbolEqualityComparer.Default.Equals(type, context.EntityType);

    private static bool IsLogic(ITypeSymbol type, GenerationContext context)
    {
        if (context.LogicType == null)
            return false;

        INamedTypeSymbol? current = type as INamedTypeSymbol;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, context.LogicType))
                return true;

            current = current.BaseType;
        }

        return false;
    }

    private static bool IsData(ITypeSymbol type, GenerationContext context) =>
        context.DataInterface != null &&
        type.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i, context.DataInterface));

    private static bool TryUnwrapDataProperty(ITypeSymbol type, out ITypeSymbol? inner)
    {
        inner = null;

        if (type is not INamedTypeSymbol named || !named.IsGenericType)
            return false;

        if (named.ConstructedFrom.ToDisplayString() != "Solas.ComponentUtils.DataProperty<T>")
            return false;

        inner = named.TypeArguments[0];
        return true;
    }

    private static bool CanBeNull(ITypeSymbol type)
    {
        if (IsNullable(type, out _))
            return true;

        if (type.IsValueType)
            return false;

        return type.NullableAnnotation != NullableAnnotation.NotAnnotated;
    }

    private static bool IsNullable(ITypeSymbol type, out ITypeSymbol? inner)
    {
        inner = null;

        if (type is not INamedTypeSymbol named || !named.IsGenericType)
            return false;

        if (named.ConstructedFrom.ToDisplayString() != "System.Nullable<T>")
            return false;

        inner = named.TypeArguments[0];
        return true;
    }

    private static bool IsEnumerable(ITypeSymbol type, out ITypeSymbol? item)
    {
        item = null;

        if (type is IArrayTypeSymbol)
            return false;

        INamedTypeSymbol? enumerable = type.AllInterfaces.FirstOrDefault(i =>
            i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");

        if (enumerable == null)
            return false;

        item = enumerable.TypeArguments[0];
        return true;
    }

    private static string Pad(int count) => new(' ', count * 4);

    private static string IndentLines(IEnumerable<string> lines, int indent)
    {
        string pad = Pad(indent);
        return string.Join("\n", lines.Select(line => $"{pad}{line}"));
    }

    private static string ToInjectMemberName(string readAccess)
    {
        const string prefix = "result.";
        if (readAccess.StartsWith(prefix, StringComparison.Ordinal))
            return readAccess[prefix.Length..];

        return readAccess;
    }

    private static string GetTypeKey(INamedTypeSymbol type, string? assemblyName) =>
        $"{type.ToDisplayString()}, {assemblyName}";

    private static IEnumerable<ISymbol> GetDeclaredMembers(INamedTypeSymbol type)
    {
        foreach (ISymbol member in type.GetMembers())
            yield return member;
    }
}
