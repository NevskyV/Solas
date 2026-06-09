using Microsoft.CodeAnalysis;
using Solas.SourceGenerators.Utils;

namespace Solas.SourceGenerators.Binary;

public static class MemberSerializationWriter
{
    public static void AppendWrite(
        string name,
        string writeAccess,
        ITypeSymbol memberType,
        CodeWriter write,
        List<BinarySerializerGenerator.InjectableField> injectableFields,
        BinarySerializerGenerator.GenerationContext context,
        string containingTypeName)
    {
        if (memberType.IsDataProperty(out var innerType))
        {
            GenerateDataPropertyWrite(write, writeAccess, innerType, context, injectableFields, name,
                containingTypeName, memberType);
            return;
        }

        GenerateWrite(write, writeAccess, memberType, context, injectableFields, name, containingTypeName);
    }

    private static void GenerateDataPropertyWrite(
        CodeWriter writer,
        string writeAccess,
        ITypeSymbol innerType,
        BinarySerializerGenerator.GenerationContext context,
        List<BinarySerializerGenerator.InjectableField> injectableFields,
        string name,
        string containingTypeName,
        ITypeSymbol memberType)
    {
        if (!memberType.CanBeNull() && memberType.CanBeNewed())
            writer.WriteLine($"{writeAccess} ??= new {memberType.ToDisplayString()}();");

        var valueAccess = $"{writeAccess}.Value";

        writer.WriteLine($"writer.Write({writeAccess} != null);");
        writer.WriteLine($"if ({writeAccess} != null)");
        writer.WriteLine("{");
        writer.Indent();

        if (!innerType.CanBeNull() && !innerType.IsValueType && innerType.CanBeNewed())
            writer.WriteLine($"{valueAccess} ??= new {innerType.ToDisplayString()}();");

        if (innerType.IsReferenceField(context))
            GenerateReferenceWrite(writer, valueAccess, innerType, context);
        else
            GenerateWrite(writer, valueAccess, innerType, context, injectableFields, name, containingTypeName);

        writer.Unindent();
        writer.WriteLine("}");
    }

    private static void GenerateWrite(
        CodeWriter writer,
        string access,
        ITypeSymbol type,
        BinarySerializerGenerator.GenerationContext context,
        List<BinarySerializerGenerator.InjectableField> injectableFields,
        string name,
        string containingTypeName)
    {
        if (type.IsNullable(out var innerNullable))
        {
            writer.WriteLine($"writer.Write({access} != null);");
            writer.WriteLine($"if ({access} != null)");
            writer.WriteLine("{");
            writer.Indent();
            GenerateWrite(writer, $"{access}.Value", innerNullable!, context, injectableFields, name,
                containingTypeName);
            writer.Unindent();
            writer.WriteLine("}");
            return;
        }

        if (type.IsReferenceField(context))
        {
            GenerateReferenceWrite(writer, access, type, context);
            return;
        }

        if (type.ToDisplayString() == "System.Guid")
        {
            writer.WriteLine($"writer.Write({access}.ToByteArray());");
            return;
        }

        if (type.ToDisplayString() == "System.Numerics.Vector3")
        {
            writer.WriteLine($"writer.Write({access}.X);");
            writer.WriteLine($"writer.Write({access}.Y);");
            writer.WriteLine($"writer.Write({access}.Z);");
            return;
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            writer.WriteLine($"writer.Write((int){access});");
            return;
        }

        if (type is ITypeParameterSymbol)
        {
            writer.WriteLine($"BinarySerializer.Write(writer, {access});");
            return;
        }

        if (type.SpecialType is not SpecialType.System_String && type is IArrayTypeSymbol array)
        {
            var nullable = type.CanBeNull();
            writer.WriteLine($"writer.Write({access} != null);");
            writer.WriteLine($"if ({access} != null)");
            writer.WriteLine("{");
            writer.Indent();

            writer.WriteLine($"writer.Write({access}.Length);");
            writer.WriteLine($"foreach (var item in {access})");
            writer.WriteLine("{");
            writer.Indent();
            GenerateWrite(writer, "item", array.ElementType, context, injectableFields, name, containingTypeName);
            writer.Unindent();
            writer.WriteLine("}");

            writer.Unindent();
            writer.WriteLine("}");

            if (!nullable)
            {
                writer.WriteLine("else");
                writer.WriteLine("{");
                writer.Indent();
                writer.WriteLine("writer.Write(0);");
                writer.Unindent();
                writer.WriteLine("}");
            }

            return;
        }

        if (type.SpecialType is not SpecialType.System_String && type.IsEnumerable(out var itemType))
        {
            var nullable = type.CanBeNull();
            writer.WriteLine($"writer.Write({access} != null);");
            writer.WriteLine($"if ({access} != null)");
            writer.WriteLine("{");
            writer.Indent();

            writer.WriteLine($"writer.Write({access}.Count());");
            writer.WriteLine($"foreach (var item in {access})");
            writer.WriteLine("{");
            writer.Indent();
            GenerateWrite(writer, "item", itemType!, context, injectableFields, name, containingTypeName);
            writer.Unindent();
            writer.WriteLine("}");

            writer.Unindent();
            writer.WriteLine("}");

            if (!nullable)
            {
                writer.WriteLine("else");
                writer.WriteLine("{");
                writer.Indent();
                writer.WriteLine("writer.Write(0);");
                writer.Unindent();
                writer.WriteLine("}");
            }

            return;
        }

        switch (type.SpecialType)
        {
            case SpecialType.System_Int32:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt32:
            case SpecialType.System_UInt64:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Char:
            case SpecialType.System_Boolean:
                writer.WriteLine($"writer.Write({access});");
                break;

            case SpecialType.System_String:
                writer.WriteLine($"writer.Write({access} != null);");
                writer.WriteLine($"if ({access} != null)");
                writer.WriteLine($"    writer.Write({access});");
                break;

            default:
                if (type.IsValueType)
                {
                    writer.WriteLine($"{type.ToDisplayString()}.Write(writer, {access});");
                }
                else
                {
                    writer.WriteLine($"writer.Write({access} != null);");
                    writer.WriteLine($"if ({access} != null)");
                    writer.WriteLine("{");
                    writer.Indent();
                    writer.WriteLine($"{type.ToDisplayString()}.Write(writer, {access});");
                    writer.Unindent();
                    writer.WriteLine("}");
                }

                break;
        }
    }

    private static void GenerateReferenceWrite(
        CodeWriter writer,
        string access,
        ITypeSymbol type,
        BinarySerializerGenerator.GenerationContext context,
        string entityParameter = "entity")
    {
        string[] statements;
        if (type.IsLogic(context))
            statements =
            [
                $"writer.Write({access}.Entity.Id.ToByteArray());",
                $"writer.Write({access}.Entity.GetSpaceId().ToByteArray());"
            ];
        else if (type.IsData(context))
            statements =
            [
                $"var owner = Solas.Query.TryGetEntityFor({access}, {entityParameter}?.CurrentSpace);",
                "writer.Write(owner.Id.ToByteArray());",
                "writer.Write(owner.GetSpaceId().ToByteArray());"
            ];
        else
            statements =
            [
                $"writer.Write({access}.Id.ToByteArray());",
                $"writer.Write({access}.GetSpaceId().ToByteArray());"
            ];

        if (type.IsValueType)
        {
            foreach (var statement in statements)
                writer.WriteLine(statement);
        }
        else
        {
            writer.WriteLine($"writer.Write({access} != null);");
            writer.WriteLine($"if ({access} != null)");
            writer.WriteLine("{");
            writer.Indent();
            foreach (var statement in statements)
                writer.WriteLine(statement);
            writer.Unindent();
            writer.WriteLine("}");
        }
    }
}