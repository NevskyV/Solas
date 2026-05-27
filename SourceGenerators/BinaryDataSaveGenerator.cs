using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Orbitality.SourceGenerators;

[Generator]
public sealed class BinarySerializerGenerator : IIncrementalGenerator
{
    private static Compilation _compilation;
    public void Initialize(
        IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<TypeDeclarationSyntax> types =
            context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (node, _) =>
                        node is TypeDeclarationSyntax,

                    static (ctx, _) =>
                        (TypeDeclarationSyntax)ctx.Node);

        IncrementalValueProvider<
            (Compilation, ImmutableArray<TypeDeclarationSyntax>)>
            compilationAndTypes =
                context.CompilationProvider.Combine(
                    types.Collect());

        context.RegisterSourceOutput(
            compilationAndTypes,
            static (ctx, source) =>
            {
                (Compilation compilation,
                    ImmutableArray<TypeDeclarationSyntax> syntaxes)
                    = source;
                _compilation = compilation;
                INamedTypeSymbol? dataInterface =
                    compilation.GetTypeByMetadataName(
                        "Orbitality.Components.IData");

                if (dataInterface == null)
                    return;

                HashSet<INamedTypeSymbol> types =
                    new(SymbolEqualityComparer.Default);

                foreach (TypeDeclarationSyntax syntax in syntaxes)
                {
                    SemanticModel semantic =
                        compilation.GetSemanticModel(
                            syntax.SyntaxTree);

                    if (semantic.GetDeclaredSymbol(syntax)
                        is not INamedTypeSymbol symbol)
                        continue;

                    if (!symbol.AllInterfaces.Any(i =>
                            SymbolEqualityComparer.Default.Equals(
                                i,
                                dataInterface)))
                        continue;

                    types.Add(symbol);
                }

                foreach (INamedTypeSymbol type in types)
                {
                    ctx.AddSource(
                        $"{type.Name}.Binary.g.cs",
                        SourceText.From(
                            GenerateSerializer(type),
                            Encoding.UTF8));
                }
            });
    }

    private static string GenerateSerializer(
        INamedTypeSymbol type)
    {
        string ns =
            type.ContainingNamespace.ToDisplayString();

        string name =
            type.Name;

        string keyword =
            type.TypeKind switch
            {
                TypeKind.Struct => "struct",
                _ => "class"
            };

        string typeParameters =
            type.TypeParameters.Length == 0
                ? ""
                : $"<{string.Join(", ",
                    type.TypeParameters.Select(
                        p => p.Name))}>";

        IEnumerable<IFieldSymbol> fields =
            type.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(static f =>
                    !f.IsStatic &&
                    !f.IsImplicitlyDeclared &&
                    f.Type.TypeKind != TypeKind.Delegate &&
                    f.DeclaredAccessibility ==
                    Accessibility.Public);

        IEnumerable<IPropertySymbol> properties =
            type.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(static p =>
                    !p.IsStatic &&
                    !p.IsImplicitlyDeclared &&
                    p.Type.TypeKind != TypeKind.Delegate &&
                    p.GetMethod != null &&
                    p.SetMethod != null &&
                    p.DeclaredAccessibility ==
                    Accessibility.Public);

        StringBuilder write = new();

        StringBuilder read = new();

        foreach (IFieldSymbol field in fields)
        {
            write.AppendLine(
                GenerateWrite(
                    $"value.{field.Name}",
                    field.Type,
                    2));

            read.AppendLine(
                GenerateRead(
                    $"result.{field.Name}",
                    field.Type,
                    2));
        }

        foreach (IPropertySymbol property in properties)
        {
            write.AppendLine(
                GenerateWrite(
                    $"value.{property.Name}",
                    property.Type,
                    2));

            read.AppendLine(
                GenerateRead(
                    $"result.{property.Name}",
                    property.Type,
                    2));
        }

        return $@"
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace {ns};

public partial {keyword} {name}{typeParameters}
{{
    public const int BinaryVersion = 1;

    public static void Write(
        BinaryWriter writer,
        {name}{typeParameters} value)
    {{
        writer.Write(BinaryVersion);
{write}
    }}

    public static {name}{typeParameters} Read(
        BinaryReader reader)
    {{
        int version = reader.ReadInt32();

        {name}{typeParameters} result = new();

{read}
        return result;
    }}
}}";
    }

    private static string GenerateWrite(
        string access,
        ITypeSymbol type,
        int indent)
    {
        string pad = Pad(indent);

        if (IsNullable(type, out ITypeSymbol? innerNullable))
        {
            return $@"
{pad}writer.Write({access} != null);

{pad}if ({access} != null)
{pad}{{
{GenerateWrite($"{access}.Value", innerNullable!, indent + 1)}
{pad}}}";
        }
        
        if (type.ToDisplayString() == "System.Type")
        {
            return
                $@"{pad}writer.Write(""{TypeToString(type)}"");";
        }
        
        if (type.ToDisplayString() == "System.Guid")
        {
            return
                $@"{pad}writer.Write({access}.ToByteArray());";
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            return $"{pad}writer.Write((int){access});";
        }

        if (type.SpecialType is not SpecialType.System_String && type is IArrayTypeSymbol array)
        {
            return $@"
{pad}writer.Write({access}.Length);

{pad}foreach (var item in {access})
{pad}{{
{GenerateWrite("item", array.ElementType, indent + 1)}
{pad}}}";
        }

        if (type.SpecialType is not SpecialType.System_String && IsEnumerable(type, out ITypeSymbol? itemType))
        {
            return $@"
{pad}writer.Write({access}.Count());

{pad}foreach (var item in {access})
{pad}{{
{GenerateWrite("item", itemType!, indent + 1)}
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

            _ => $"{pad}{type.ToDisplayString()}.Write(writer, {access});"
        };
    }

    private static string GenerateRead(
        string access,
        ITypeSymbol type,
        int indent)
    {
        string pad = Pad(indent);

        if (IsNullable(type, out ITypeSymbol? innerNullable))
        {
            return $@"
{pad}if (reader.ReadBoolean())
{pad}{{
{GenerateRead(access, innerNullable!, indent + 1)}
{pad}}}";
        }


        if (type.ToDisplayString() == "System.Type")
        {
            return
                $"{pad}{access} = Type.GetType(reader.ReadString());";
        }
        
        if (type.ToDisplayString() == "System.Guid")
        {
            return
                $"{pad}{access} = new Guid(reader.ReadBytes(16));";
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            return
                $"{pad}{access} = ({type.ToDisplayString()})reader.ReadInt32();";
        }

        if (type.SpecialType is not SpecialType.System_String && type is IArrayTypeSymbol array)
        {
            string element =
                array.ElementType.ToDisplayString();

            return $@"
{pad}int length = reader.ReadInt32();

{pad}{access} = new {element}[length];

{pad}for (int i = 0; i < length; i++)
{pad}{{
{GenerateRead($"{access}[i]", array.ElementType, indent + 1)}
{pad}}}";
        }

        if (type.SpecialType is not SpecialType.System_String && IsEnumerable(type, out ITypeSymbol? itemType))
        {
            string collection =
                type.ToDisplayString();

            string item =
                itemType!.ToDisplayString();

            return $@"
{pad}int count = reader.ReadInt32();

{pad}{access} = new {collection}();

{pad}for (int i = 0; i < count; i++)
{pad}{{
{pad}    {item} value = default!;

{GenerateRead("value", itemType, indent + 1)}

{pad}    {access}.Add(value);
{pad}}}";
        }

        return type.SpecialType switch
        {
            SpecialType.System_Int32
                => $"{pad}{access} = reader.ReadInt32();",

            SpecialType.System_Single
                => $"{pad}{access} = reader.ReadSingle();",

            SpecialType.System_Double
                => $"{pad}{access} = reader.ReadDouble();",

            SpecialType.System_Int64
                => $"{pad}{access} = reader.ReadInt64();",

            SpecialType.System_UInt32
                => $"{pad}{access} = reader.ReadUInt32();",

            SpecialType.System_UInt64
                => $"{pad}{access} = reader.ReadUInt64();",

            SpecialType.System_Int16
                => $"{pad}{access} = reader.ReadInt16();",

            SpecialType.System_UInt16
                => $"{pad}{access} = reader.ReadUInt16();",

            SpecialType.System_Byte
                => $"{pad}{access} = reader.ReadByte();",

            SpecialType.System_SByte
                => $"{pad}{access} = reader.ReadSByte();",

            SpecialType.System_Char
                => $"{pad}{access} = reader.ReadChar();",

            SpecialType.System_Boolean
                => $"{pad}{access} = reader.ReadBoolean();",

            SpecialType.System_String
                => $"{pad}{access} = reader.ReadString();",

            _ =>
                $"{pad}{access} = {type.ToDisplayString()}.Read(reader);"
        };
    }

    private static bool IsNullable(
        ITypeSymbol type,
        out ITypeSymbol? inner)
    {
        inner = null;

        if (type is not INamedTypeSymbol named)
            return false;

        if (!named.IsGenericType)
            return false;

        if (named.ConstructedFrom.ToDisplayString()
            != "System.Nullable<T>")
            return false;

        inner = named.TypeArguments[0];

        return true;
    }

    private static bool IsEnumerable(
        ITypeSymbol type,
        out ITypeSymbol? item)
    {
        item = null;

        if (type is IArrayTypeSymbol)
            return false;

        INamedTypeSymbol? enumerable =
            type.AllInterfaces.FirstOrDefault(i =>
                i.OriginalDefinition.ToDisplayString()
                == "System.Collections.Generic.IEnumerable<T>");

        if (enumerable == null)
            return false;

        item = enumerable.TypeArguments[0];

        return true;
    }

    private static string Pad(int count)
    {
        return new string(' ', count * 4);
    }

    private static string TypeToString(ITypeSymbol symbol)
    {
        var fullName = symbol.ToDisplayString();
        return $"{fullName}, {_compilation.AssemblyName}";
    }
}