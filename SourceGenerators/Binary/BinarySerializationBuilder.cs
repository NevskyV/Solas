using Microsoft.CodeAnalysis;
using Solas.SourceGenerators.Utils;

namespace Solas.SourceGenerators.Binary;

public static class BinarySerializationBuilder
{
    public static string Generate(INamedTypeSymbol type, BinarySerializerGenerator.GenerationContext context,
        string? assemblyName)
    {
        var writer = new CodeWriter();
        var ns = type.ContainingNamespace.ToDisplayString();
        var name = type.Name;
        var typeKey = $"{type.ToDisplayString()}, {assemblyName}";
        var isStruct = type.TypeKind == TypeKind.Struct;
        var keyword = isStruct ? "struct" : "class";

        var typeParameters = type.TypeParameters.Length == 0
            ? ""
            : $"<{string.Join(", ", type.TypeParameters.Select(p => p.Name))}>";

        var constraints = GetConstraints(type);

        writer.WriteLine("using System;");
        writer.WriteLine("using System.Collections.Generic;");
        writer.WriteLine("using System.IO;");
        writer.WriteLine("using System.Linq;");
        writer.WriteLine("using System.Runtime.CompilerServices;");
        writer.WriteLine("using Solas.Components;");
        writer.WriteLine("using Solas.Interfaces;");
        writer.WriteLine("using Solas.Serialization;");
        writer.WriteLine();
        writer.WriteLine($"namespace {ns};");
        writer.WriteLine();

        var binaryClassName = $"{name}Binary";
        var serializableMembers = GetSerializableMembers(type);

        var injectableFields = new List<BinarySerializerGenerator.InjectableField>();
        foreach (var member in serializableMembers)
            if (member.Type.IsDataProperty(out var innerType))
            {
                if (innerType!.IsReferenceField(context))
                    injectableFields.Add(new BinarySerializerGenerator.InjectableField
                    {
                        Name = member.Name,
                        Type = innerType,
                        IsDataProperty = true
                    });
            }
            else if (member.Type.IsReferenceField(context))
            {
                injectableFields.Add(new BinarySerializerGenerator.InjectableField
                {
                    Name = member.Name,
                    Type = member.Type,
                    IsDataProperty = false
                });
            }

        var writeBuilder = new CodeWriter();
        var readBuilder = new CodeWriter();

        foreach (var member in serializableMembers)
        {
            MemberSerializationWriter.AppendWrite(
                member.Name,
                $"value.{member.Name}",
                member.Type,
                writeBuilder,
                injectableFields,
                context,
                type.Name);

            MemberSerializationReader.AppendRead(
                member.Name,
                $"result.{member.Name}",
                member.Type,
                readBuilder,
                injectableFields,
                context,
                type.Name);
        }

        if (isStruct)
        {
            writer.WriteLine($"internal static class {binaryClassName}");
            writer.WriteLine("{");
            writer.Indent();
            GenerateRegisterMethod(writer, name, typeParameters, typeKey);
            GenerateSerializationMethods(writer, name, typeParameters, writeBuilder.ToString(), readBuilder.ToString());
            GenerateReadMethod(writer, name, typeParameters, true);
            writer.Unindent();
            writer.WriteLine("}");
            writer.WriteLine();

            writer.WriteLine($"public partial {keyword} {name}{typeParameters}{constraints}");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine("public (Guid, Guid)[] SerializationGuids => Array.Empty<(Guid, Guid)>();");
            writer.WriteLine();
            writer.WriteLine(
                $"public void Write(BinaryWriter writer, Entity entity) => {binaryClassName}.Write(writer, this, entity);");
            writer.WriteLine();
            writer.WriteLine(
                $"public static void Write(BinaryWriter writer, {name}{typeParameters} value, Entity entity = null) => {binaryClassName}.Write(writer, value, entity);");
            GenerateInjectMethod(writer, injectableFields, context);
            writer.Unindent();
            writer.WriteLine("}");
        }
        else
        {
            writer.WriteLine($"public partial {keyword} {name}{typeParameters}{constraints}");
            writer.WriteLine("{");
            writer.Indent();
            GenerateRegisterMethod(writer, name, typeParameters, typeKey);
            writer.WriteLine("private (Guid, Guid)[] _serializationGuids = Array.Empty<(Guid, Guid)>();");
            writer.WriteLine();
            writer.WriteLine("public (Guid, Guid)[] SerializationGuids => _serializationGuids;");
            writer.WriteLine();
            writer.WriteLine("public void Write(BinaryWriter writer, Entity entity) => Write(writer, this, entity);");
            GenerateSerializationMethods(writer, name, typeParameters, writeBuilder.ToString(), readBuilder.ToString());
            GenerateReadMethod(writer, name, typeParameters, false);
            GenerateInjectMethod(writer, injectableFields, context);
            writer.Unindent();
            writer.WriteLine("}");
        }

        return writer.ToString();
    }

    private static List<SerializableMember> GetSerializableMembers(INamedTypeSymbol type)
    {
        var members = new List<SerializableMember>();

        foreach (var field in type.GetMembers().OfType<IFieldSymbol>()
                     .Where(f => !f.IsStatic &&
                                 !f.IsImplicitlyDeclared &&
                                 f.Type.TypeKind != TypeKind.Delegate &&
                                 f.DeclaredAccessibility == Accessibility.Public))
            members.Add(new SerializableMember(field.Name, field.Type));

        foreach (var property in type.GetMembers().OfType<IPropertySymbol>()
                     .Where(p => !p.IsStatic &&
                                 !p.IsImplicitlyDeclared &&
                                 p.Type.TypeKind != TypeKind.Delegate &&
                                 p.GetMethod != null &&
                                 p.SetMethod != null &&
                                 p.DeclaredAccessibility == Accessibility.Public))
            members.Add(new SerializableMember(property.Name, property.Type));

        return members;
    }

    private static void GenerateRegisterMethod(CodeWriter writer, string name, string typeParameters, string typeKey)
    {
        writer.WriteLine("[ModuleInitializer]");
        writer.WriteLine("internal static void RegisterBinarySerialization()");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine($"DataSerializationRegistry.Register<{name}{typeParameters}>(");
        writer.Indent();
        writer.WriteLine($"\"{typeKey}\",");
        writer.WriteLine("Write,");
        writer.WriteLine("ReadInternal);");
        writer.Unindent();
        writer.Unindent();
        writer.WriteLine("}");
        writer.WriteLine();
    }

    private static void GenerateSerializationMethods(
        CodeWriter writer,
        string name,
        string typeParameters,
        string writeBody,
        string readBody)
    {
        writer.WriteLine("public const int BinaryVersion = 1;");
        writer.WriteLine();
        writer.WriteLine(
            $"public static void Write(BinaryWriter writer, {name}{typeParameters} value, Entity entity = null)");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine("writer.Write(BinaryVersion);");
        writer.WriteLines(writeBody);
        writer.Unindent();
        writer.WriteLine("}");
        writer.WriteLine();

        writer.WriteLine(
            $"public static ({name}{typeParameters} data, (Guid, Guid)[] guids) ReadInternal(BinaryReader reader)");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine("_ = reader.ReadInt32();");
        writer.WriteLine($"{name}{typeParameters} result = new {name}{typeParameters}();");
        writer.WriteLine("var guids = new List<(Guid, Guid)>();");
        writer.WriteLine();
        writer.WriteLines(readBody);
        writer.WriteLine("return (result, guids.ToArray());");
        writer.Unindent();
        writer.WriteLine("}");
        writer.WriteLine();
    }

    private static void GenerateReadMethod(CodeWriter writer, string name, string typeParameters, bool isStruct)
    {
        if (isStruct)
        {
            writer.WriteLine(
                $"public static {name}{typeParameters} Read(BinaryReader reader) => ReadInternal(reader).data;");
        }
        else
        {
            writer.WriteLine($"public static {name}{typeParameters} Read(BinaryReader reader)");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine("var (data, guids) = ReadInternal(reader);");
            writer.WriteLine("data._serializationGuids = guids;");
            writer.WriteLine("return data;");
            writer.Unindent();
            writer.WriteLine("}");
        }
    }

    private static void GenerateInjectMethod(CodeWriter writer,
        List<BinarySerializerGenerator.InjectableField> injectableFields,
        BinarySerializerGenerator.GenerationContext context)
    {
        if (injectableFields.Count == 0) return;

        writer.WriteLine();
        writer.WriteLine("public void Inject((Guid, Guid)[] guids)");
        writer.WriteLine("{");
        writer.Indent();

        var index = 0;
        foreach (var field in injectableFields) writer.WriteLine(GenerateInjectAssignment(field, index++, context));

        writer.Unindent();
        writer.WriteLine("}");
    }

    private static string GenerateInjectAssignment(BinarySerializerGenerator.InjectableField field, int index,
        BinarySerializerGenerator.GenerationContext context)
    {
        var access = field.IsDataProperty ? $"{field.Name}.Value" : field.Name;

        if (field.Type.IsEntity(context))
            return $"{access} = Solas.Command.Inject(guids[{index}].Item1, guids[{index}].Item2);";
        if (field.Type.IsLogic(context))
            return
                $"{access} = Solas.Command.Inject(guids[{index}].Item1, guids[{index}].Item2).GetLogic<{field.Type.ToDisplayString()}>();";
        if (field.Type.IsData(context))
            return
                $"{access} = Solas.Command.Inject(guids[{index}].Item1, guids[{index}].Item2).GetData<{field.Type.ToDisplayString()}>();";
        return
            $"{access} = Solas.Command.Inject<{field.Type.ToDisplayString()}>(guids[{index}].Item1, guids[{index}].Item2);";
    }

    private static string GetConstraints(INamedTypeSymbol type)
    {
        var constraintsList = type.TypeParameters.Select(p =>
        {
            List<string> parts = [];
            if (p.HasReferenceTypeConstraint) parts.Add("class");
            if (p.HasValueTypeConstraint) parts.Add("struct");
            foreach (var constraint in p.ConstraintTypes)
                parts.Add(constraint.ToDisplayString());
            if (p.HasConstructorConstraint) parts.Add("new()");

            return parts.Count > 0 ? $"where {p.Name} : {string.Join(", ", parts)}" : "";
        }).Where(s => !string.IsNullOrEmpty(s));

        var enumerable = constraintsList as string[] ?? constraintsList.ToArray();
        return enumerable.Length != 0 ? "\n" + string.Join("\n", enumerable) : "";
    }

    private record SerializableMember(string Name, ITypeSymbol Type);
}