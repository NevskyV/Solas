using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Solas.SourceGenerators.Utils;

namespace Solas.SourceGenerators.Binary;

public static class MemberSerializationReader
{
    public static void AppendRead(
        string name,
        string readAccess,
        ITypeSymbol memberType,
        CodeWriter read,
        List<BinarySerializerGenerator.InjectableField> injectableFields,
        BinarySerializerGenerator.GenerationContext context,
        string containingTypeName)
    {
        if (memberType.IsDataProperty(out ITypeSymbol? innerType))
        {
            GenerateDataPropertyRead(read, readAccess, innerType, context, injectableFields, name, containingTypeName, memberType);
            return;
        }

        GenerateRead(read, readAccess, memberType, context, injectableFields, name, containingTypeName);
    }

    private static void GenerateDataPropertyRead(
        CodeWriter writer,
        string readAccess,
        ITypeSymbol innerType,
        BinarySerializerGenerator.GenerationContext context,
        List<BinarySerializerGenerator.InjectableField> injectableFields,
        string name,
        string containingTypeName,
        ITypeSymbol memberType)
    {
        writer.WriteLine("if (reader.ReadBoolean())");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine($"{readAccess} ??= new Solas.ComponentUtils.DataProperty<{innerType.ToDisplayString()}>();");

        string valueAccess = $"{readAccess}.Value";

        if (!innerType.CanBeNull() && !innerType.IsValueType && innerType.CanBeNewed())
        {
            writer.WriteLine($"{valueAccess} ??= new {innerType.ToDisplayString()}();");
        }

        if (innerType.IsReferenceField(context))
        {
            GenerateReferenceRead(writer, innerType, context);
        }
        else
        {
            GenerateRead(writer, valueAccess, innerType, context, injectableFields, name, containingTypeName);
        }

        writer.Unindent();
        writer.WriteLine("}");
        writer.WriteLine("else");
        writer.WriteLine("{");
        writer.Indent();

        if (!memberType.CanBeNull() && memberType.CanBeNewed())
        {
            writer.WriteLine($"{readAccess} = new {memberType.ToDisplayString()}();");
        }
        else
        {
            writer.WriteLine($"{readAccess} = null;");
        }

        if (innerType.IsReferenceField(context))
        {
            writer.WriteLine("guids.Add((Guid.Empty, Guid.Empty));");
        }

        writer.Unindent();
        writer.WriteLine("}");
    }

    public static void GenerateRead(
        CodeWriter writer,
        string access,
        ITypeSymbol type,
        BinarySerializerGenerator.GenerationContext context,
        List<BinarySerializerGenerator.InjectableField> injectableFields,
        string name,
        string containingTypeName)
    {
        if (type.IsNullable(out ITypeSymbol? innerNullable))
        {
            writer.WriteLine("if (reader.ReadBoolean())");
            writer.WriteLine("{");
            writer.Indent();
            GenerateRead(writer, access, innerNullable!, context, injectableFields, name, containingTypeName);
            writer.Unindent();
            writer.WriteLine("}");
            return;
        }

        if (type.IsReferenceField(context))
        {
            GenerateReferenceRead(writer, type, context);
            return;
        }

        if (type.ToDisplayString() == "System.Guid")
        {
            writer.WriteLine($"{access} = new Guid(reader.ReadBytes(16));");
            return;
        }

        if (type.ToDisplayString() == "System.Numerics.Vector3")
        {
            writer.WriteLine($"{access} = new System.Numerics.Vector3(");
            writer.Indent();
            writer.WriteLine("reader.ReadSingle(),");
            writer.WriteLine("reader.ReadSingle(),");
            writer.WriteLine("reader.ReadSingle());");
            writer.Unindent();
            return;
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            writer.WriteLine($"{access} = ({type.ToDisplayString()})reader.ReadInt32();");
            return;
        }

        if (type is ITypeParameterSymbol)
        {
            writer.WriteLine($"{access} = ({type.ToDisplayString()})BinarySerializer.Read(reader, typeof({type.ToDisplayString()}));");
            return;
        }

        if (type.SpecialType is not SpecialType.System_String && type is IArrayTypeSymbol array)
        {
            writer.WriteLine("if (reader.ReadBoolean())");
            writer.WriteLine("{");
            writer.Indent();

            writer.WriteLine("int length = reader.ReadInt32();");
            writer.WriteLine($"{access} = new {array.ElementType.ToDisplayString()}[length];");
            writer.WriteLine("for (int i = 0; i < length; i++)");
            writer.WriteLine("{");
            writer.Indent();
            GenerateRead(writer, $"{access}[i]", array.ElementType, context, injectableFields, name, containingTypeName);
            writer.Unindent();
            writer.WriteLine("}");

            writer.Unindent();
            writer.WriteLine("}");
            writer.WriteLine("else");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine($"{access} = null;");
            writer.Unindent();
            writer.WriteLine("}");
            return;
        }

        if (type.SpecialType is not SpecialType.System_String && type.IsEnumerable(out ITypeSymbol? itemType))
        {
            writer.WriteLine("if (reader.ReadBoolean())");
            writer.WriteLine("{");
            writer.Indent();

            string collection = type.ToDisplayString();
            string item = itemType!.ToDisplayString();

            writer.WriteLine("int count = reader.ReadInt32();");
            writer.WriteLine($"{access} = new {collection}();");
            writer.WriteLine("for (int i = 0; i < count; i++)");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine($"{item} value = default!;");
            GenerateRead(writer, "value", itemType, context, injectableFields, name, containingTypeName);
            writer.WriteLine($"{access}.Add(value);");
            writer.Unindent();
            writer.WriteLine("}");

            writer.Unindent();
            writer.WriteLine("}");
            writer.WriteLine("else");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine($"{access} = null;");
            writer.Unindent();
            writer.WriteLine("}");
            return;
        }

        switch (type.SpecialType)
        {
            case SpecialType.System_Int32:
                writer.WriteLine($"{access} = reader.ReadInt32();");
                break;
            case SpecialType.System_Single:
                writer.WriteLine($"{access} = reader.ReadSingle();");
                break;
            case SpecialType.System_Double:
                writer.WriteLine($"{access} = reader.ReadDouble();");
                break;
            case SpecialType.System_Int64:
                writer.WriteLine($"{access} = reader.ReadInt64();");
                break;
            case SpecialType.System_UInt32:
                writer.WriteLine($"{access} = reader.ReadUInt32();");
                break;
            case SpecialType.System_UInt64:
                writer.WriteLine($"{access} = reader.ReadUInt64();");
                break;
            case SpecialType.System_Int16:
                writer.WriteLine($"{access} = reader.ReadInt16();");
                break;
            case SpecialType.System_UInt16:
                writer.WriteLine($"{access} = reader.ReadUInt16();");
                break;
            case SpecialType.System_Byte:
                writer.WriteLine($"{access} = reader.ReadByte();");
                break;
            case SpecialType.System_SByte:
                writer.WriteLine($"{access} = reader.ReadSByte();");
                break;
            case SpecialType.System_Char:
                writer.WriteLine($"{access} = reader.ReadChar();");
                break;
            case SpecialType.System_Boolean:
                writer.WriteLine($"{access} = reader.ReadBoolean();");
                break;
            case SpecialType.System_String:
                writer.WriteLine("if (reader.ReadBoolean())");
                writer.WriteLine($"    {access} = reader.ReadString();");
                writer.WriteLine("else");
                writer.WriteLine($"    {access} = string.Empty;");
                break;

            default:
                if (type.IsValueType)
                {
                    writer.WriteLine($"{access} = {type.ToDisplayString()}.Read(reader);");
                }
                else
                {
                    writer.WriteLine("if (reader.ReadBoolean())");
                    writer.WriteLine("{");
                    writer.Indent();
                    writer.WriteLine($"{access} = {type.ToDisplayString()}.Read(reader);");
                    writer.Unindent();
                    writer.WriteLine("}");
                    writer.WriteLine("else");
                    writer.WriteLine("{");
                    writer.Indent();
                    writer.WriteLine($"{access} = null;");
                    writer.Unindent();
                    writer.WriteLine("}");
                }
                break;
        }
    }

    private static void GenerateReferenceRead(CodeWriter writer, ITypeSymbol type, BinarySerializerGenerator.GenerationContext context)
    {
        if (type.IsValueType)
        {
            writer.WriteLine("guids.Add((new Guid(reader.ReadBytes(16)), new Guid(reader.ReadBytes(16))));");
        }
        else
        {
            writer.WriteLine("if (reader.ReadBoolean())");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine("guids.Add((new Guid(reader.ReadBytes(16)), new Guid(reader.ReadBytes(16))));");
            writer.Unindent();
            writer.WriteLine("}");
            writer.WriteLine("else");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine("guids.Add((Guid.Empty, Guid.Empty));");
            writer.Unindent();
            writer.WriteLine("}");
        }
    }
}