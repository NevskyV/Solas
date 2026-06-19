using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Solas.SourceGenerators.Utils;

namespace Solas.SourceGenerators;

[Generator]
public sealed class SerializationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var types = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => ctx.Node as TypeDeclarationSyntax
        ).Where(static t => t is not null);

        var compilationAndTypes = context.CompilationProvider.Combine(types.Collect());

        context.RegisterSourceOutput(compilationAndTypes, static (ctx, source) =>
        {
            var (compilation, syntaxes) = source;
            var assemblyName = compilation.AssemblyName ?? "UnknownAssembly";

            var dataInterface = compilation.GetTypeByMetadataName("Solas.Components.IData");
            var logicBaseType = compilation.GetTypeByMetadataName("Solas.Components.Logic");
            var referenceableInterface = compilation.GetTypeByMetadataName("Solas.Interfaces.IReferenceable");

            var customSerializerInterface =
                compilation.GetTypeByMetadataName("Solas.Serialization.Core.ICustomSerializer`1")
                ?? compilation.GetTypeByMetadataName("Solas.Serialization.ICustomSerializer`1");

            if (dataInterface == null) return;

            var userCustomSerializers = new HashSet<string>();
            var candidatesForGeneration = new List<INamedTypeSymbol>();
            var allSerializers = new List<(TypeMetadata Type, string SerializerName)>();

            foreach (var syntax in syntaxes)
            {
                var model = compilation.GetSemanticModel(syntax!.SyntaxTree);
                if (model.GetDeclaredSymbol(syntax) is not INamedTypeSymbol symbol) continue;

                if (customSerializerInterface != null)
                {
                    var serializerImpl = symbol.AllInterfaces.FirstOrDefault(i =>
                        SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, customSerializerInterface));

                    if (serializerImpl != null)
                    {
                        var targetType = serializerImpl.TypeArguments[0];
                        var targetTypeFullName = targetType.ToDisplayString();

                        if (userCustomSerializers.Add(targetTypeFullName))
                        {
                            var targetAssembly = targetType.ContainingAssembly?.Name ?? assemblyName;

                            var customMeta = new TypeMetadata(
                                targetType.Name,
                                targetTypeFullName,
                                targetType.ContainingNamespace.ToDisplayString(),
                                targetAssembly,
                                targetType.TypeKind == TypeKind.Struct,
                                targetType.IsValueType
                            );

                            allSerializers.Add((customMeta, symbol.ToDisplayString()));
                        }

                        continue;
                    }
                }

                var isData = symbol.ImplementsInterface(dataInterface);
                var isLogic = logicBaseType != null && symbol.InheritsFrom(logicBaseType);

                if ((isData || isLogic) && !symbol.IsAbstract && symbol.TypeKind is TypeKind.Class or TypeKind.Struct)
                {
                    var injectSource = DependencyInjectionBuilder.Generate(symbol, dataInterface, logicBaseType,
                        referenceableInterface);
                    if (injectSource != null)
                        ctx.AddSource($"{symbol.Name}.Inject.g.cs", SourceText.From(injectSource, Encoding.UTF8));

                    if (isData) candidatesForGeneration.Add(symbol);
                }
            }

            var processedTypes = new HashSet<string>(userCustomSerializers);
            var queue = new Queue<INamedTypeSymbol>(candidatesForGeneration);

            while (queue.Count > 0)
            {
                var symbol = queue.Dequeue();
                var fullTypeName = symbol.ToDisplayString();

                if (!processedTypes.Add(fullTypeName)) continue;

                var metadata = new TypeMetadata(
                    symbol.Name,
                    fullTypeName,
                    symbol.ContainingNamespace.ToDisplayString(),
                    assemblyName,
                    symbol.TypeKind == TypeKind.Struct,
                    symbol.IsValueType
                );

                var members = GetSerializableMembers(symbol);
                var generatedSource = GenerateSerializerCode(metadata, members);

                var sanitizedName = GetSanitizedTypeName(symbol);
                var serializerName = $"{sanitizedName}Serializer";

                ctx.AddSource($"{serializerName}.g.cs", SourceText.From(generatedSource, Encoding.UTF8));
                allSerializers.Add((metadata, $"Solas.Generated.{serializerName}"));

                foreach (var member in symbol.GetMembers().OfType<IFieldSymbol>())
                    EnqueueMemberType(member.Type, queue);
                foreach (var member in symbol.GetMembers().OfType<IPropertySymbol>())
                    EnqueueMemberType(member.Type, queue);
            }

            var registrySource = GenerateRegistryCode(allSerializers);
            ctx.AddSource("SerializationRegistration.g.cs", SourceText.From(registrySource, Encoding.UTF8));
        });
    }

    private static void EnqueueMemberType(ITypeSymbol type, Queue<INamedTypeSymbol> queue)
    {
        var memberType = type;
        if (memberType is IArrayTypeSymbol arrayType) memberType = arrayType.ElementType;

        if (memberType is INamedTypeSymbol namedMemberType)
        {
            if (namedMemberType.IsDataProperty(out var innerType))
            {
                if (innerType is INamedTypeSymbol namedInner && !namedInner.IsPrimitive() &&
                    namedInner.TypeKind is TypeKind.Class or TypeKind.Struct) queue.Enqueue(namedInner);
                return;
            }

            if (!namedMemberType.IsPrimitive() && namedMemberType.TypeKind is TypeKind.Class or TypeKind.Struct)
                queue.Enqueue(namedMemberType);
        }
    }

    private static string GetSanitizedTypeName(INamedTypeSymbol symbol)
    {
        if (!symbol.IsGenericType)
            return symbol.Name;

        var sb = new StringBuilder();
        sb.Append(symbol.Name);
        foreach (var arg in symbol.TypeArguments)
        {
            sb.Append('_');
            sb.Append(arg.Name);
        }

        return sb.ToString();
    }

    private static List<MemberMetadata> GetSerializableMembers(INamedTypeSymbol type)
    {
        var members = new List<MemberMetadata>();

        var fields = type.GetMembers().OfType<IFieldSymbol>()
            .Where(f => !f.IsStatic && !f.IsImplicitlyDeclared && f.DeclaredAccessibility == Accessibility.Public);

        foreach (var f in fields) members.Add(CreateMemberMetadata(f.Name, f.Type));

        var properties = type.GetMembers().OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic && p.GetMethod != null && p.SetMethod != null &&
                        p.DeclaredAccessibility == Accessibility.Public);

        foreach (var p in properties) members.Add(CreateMemberMetadata(p.Name, p.Type));

        return members;
    }

    private static MemberMetadata CreateMemberMetadata(string name, ITypeSymbol type)
    {
        var isArray = type is IArrayTypeSymbol;
        var elementType = isArray ? ((IArrayTypeSymbol)type).ElementType : type;

        return new MemberMetadata(
            name,
            type.ToDisplayString(),
            isArray,
            elementType.ToDisplayString(),
            elementType.IsPrimitive(),
            type.NullableAnnotation == NullableAnnotation.Annotated
        );
    }

    private static string GenerateSerializerCode(TypeMetadata type, List<MemberMetadata> members)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using Solas;");
        sb.AppendLine("using Solas.Serialization.Core;");
        sb.AppendLine($"using {type.Namespace};");
        sb.AppendLine();
        sb.AppendLine("namespace Solas.Generated;");
        sb.AppendLine();
        sb.AppendLine($"public sealed class {type.Name}Serializer : ICustomSerializer<{type.FullName}>");
        sb.AppendLine("{");

        sb.AppendLine($"    public void Write({type.FullName} value, FileStream stream, string name = null)");
        sb.AppendLine("    {");
        sb.AppendLine("        Query.Serializer.BeginObject(stream);");
        foreach (var member in members)
            if (member.IsNullable)
            {
                sb.AppendLine($"        Query.Serializer.Write(value.{member.Name} != null, stream);");
                sb.AppendLine($"        if (value.{member.Name} != null)");
                sb.AppendLine("        {");
                if (member.IsArray)
                {
                    if (member.IsPrimitive)
                        sb.AppendLine(
                            $"            Query.Serializer.WriteArray(value.{member.Name}, stream, Query.Serializer.Write);");
                    else
                        sb.AppendLine($"            Query.Serializer.WriteArray(value.{member.Name}, stream);");
                }
                else
                {
                    sb.AppendLine($"            Query.Serializer.Write(value.{member.Name}, stream);");
                }

                sb.AppendLine("        }");
            }
            else
            {
                if (member.IsArray)
                {
                    if (member.IsPrimitive)
                        sb.AppendLine(
                            $"        Query.Serializer.WriteArray(value.{member.Name}, stream, Query.Serializer.Write);");
                    else
                        sb.AppendLine($"        Query.Serializer.WriteArray(value.{member.Name}, stream);");
                }
                else
                {
                    sb.AppendLine($"        Query.Serializer.Write(value.{member.Name}, stream);");
                }
            }

        sb.AppendLine("        Query.Serializer.EndObject(stream);");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine($"    public {type.FullName} Read(FileStream stream)");
        sb.AppendLine("    {");
        if (type.IsStruct)
            sb.AppendLine($"        {type.FullName} result = default;");
        else
            sb.AppendLine($"        {type.FullName} result = new {type.FullName}();");
        foreach (var member in members)
            if (member.IsNullable)
            {
                sb.AppendLine("        if (Query.Serializer.ReadBool(stream))");
                sb.AppendLine("        {");
                sb.AppendLine($"            result.{member.Name} = {GenerateReadCall(member)};");
                sb.AppendLine("        }");
            }
            else
            {
                sb.AppendLine($"        result.{member.Name} = {GenerateReadCall(member)};");
            }

        sb.AppendLine("        return result;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateReadCall(MemberMetadata member)
    {
        if (member.IsArray)
            return member.IsPrimitive
                ? $"Query.Serializer.ReadArray(stream, Query.Serializer.Read{GetPrimitiveMethodSuffix(member.ElementTypeFullName)})"
                : $"Query.Serializer.ReadArray<{member.ElementTypeFullName}>(stream)";

        return member.IsPrimitive
            ? $"Query.Serializer.Read{GetPrimitiveMethodSuffix(member.TypeFullName)}(stream)"
            : $"Query.Serializer.Read<{member.TypeFullName}>(stream)";
    }

    private static string GetPrimitiveMethodSuffix(string fullName)
    {
        return fullName switch
        {
            "byte" => "Byte",
            "bool" => "Bool",
            "char" => "Char",
            "string" => "String",
            "short" => "Int16",
            "int" => "Int32",
            "long" => "Int64",
            "ushort" => "UInt16",
            "uint" => "UInt32",
            "ulong" => "UInt64",
            "float" => "Float",
            "double" => "Double",
            "System.Guid" => "Guid",
            _ => "Int32"
        };
    }

    private static string GenerateRegistryCode(List<(TypeMetadata Type, string SerializerName)> serializers)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Solas.Serialization.Core;");
        sb.AppendLine();
        sb.AppendLine("namespace Solas.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class SerializationRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    public static void Add(Serializer serializer)");
        sb.AppendLine("    {");
        foreach (var s in serializers)
        {
            var key = $"{s.Type.FullName}, {s.Type.AssemblyName}";
            sb.AppendLine($"        serializer.AddSerializer<{s.Type.FullName}>(new {s.SerializerName}());");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}