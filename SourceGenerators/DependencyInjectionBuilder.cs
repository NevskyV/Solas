using System.Text;
using Microsoft.CodeAnalysis;
using Solas.SourceGenerators.Utils;

namespace Solas.SourceGenerators;

public enum InjectType
{
    Inject,
    AutoInject
}

public enum ReferenceKind
{
    IData,
    Logic,
    Referenceable
}

public struct InjectableMember
{
    public string Name;
    public string TypeFullName;
    public InjectType InjectType;
    public ReferenceKind ReferenceKind;
}

public static class DependencyInjectionBuilder
{
    public static string? Generate(
        INamedTypeSymbol symbol,
        INamedTypeSymbol? dataInterface,
        INamedTypeSymbol? logicBaseType,
        INamedTypeSymbol? referenceableInterface)
    {
        var ns = symbol.ContainingNamespace.ToDisplayString();
        var name = symbol.Name;
        var isStruct = symbol.TypeKind == TypeKind.Struct;
        var keyword = isStruct ? "struct" : "class";
        var overrideAttribute = symbol.InheritsFrom(logicBaseType) ? "override " : "";

        var injectableMembers = CollectInjectableMembers(symbol, dataInterface, logicBaseType, referenceableInterface);
        if (injectableMembers.Count == 0)
            return null;
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using Solas;");
        sb.AppendLine("using Solas.Components;");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine($"public partial {keyword} {name}");
        sb.AppendLine("{");

        var serializableMembers = injectableMembers.Where(m => m.InjectType == InjectType.Inject).ToList();

        sb.AppendLine($"    public {overrideAttribute}void WriteInject(FileStream stream, Entity entity = null)");
        sb.AppendLine("    {");
        sb.AppendLine($"            var injectables = Query.LastInjectables;");
        var membersCount = 0;
        foreach (var member in serializableMembers)
        {
            sb.AppendLine($"        if (this.{member.Name} == null)");
            sb.AppendLine("        {");
            
            sb.AppendLine($"            Query.Serializer.Write(injectables[{membersCount}].Item1, stream, \"{member.Name}_Id\");");
            sb.AppendLine($"            Query.Serializer.Write(injectables[{membersCount}].Item2, stream, \"{member.Name}_SpaceId\");");
            sb.AppendLine("        }");
            sb.AppendLine("        else");
            sb.AppendLine("        {");

            if (member.ReferenceKind == ReferenceKind.Logic)
            {
                sb.AppendLine($"            Query.Serializer.Write(this.{member.Name}.Entity.Id, stream, \"{member.Name}_Id\");");
                sb.AppendLine($"            Query.Serializer.Write(this.{member.Name}.Entity.GetSpaceId(), stream, \"{member.Name}_SpaceId\");");
            }
            else if (member.ReferenceKind == ReferenceKind.IData)
            {
                sb.AppendLine(
                    $"            var owner = Solas.Query.TryGetEntityFor(this.{member.Name}, entity?.CurrentSpace);");
                sb.AppendLine($"            Query.Serializer.Write(owner != null ? owner.Id : Guid.Empty, stream, \"{member.Name}_Id\");");
                sb.AppendLine(
                    $"            Query.Serializer.Write(owner != null ? owner.GetSpaceId() : Guid.Empty, stream, \"{member.Name}_SpaceId\");");
            }
            else
            {
                sb.AppendLine($"            Query.Serializer.Write(this.{member.Name}.Id, stream, \"{member.Name}_Id\");");
                sb.AppendLine($"            Query.Serializer.Write(this.{member.Name}.GetSpaceId(), stream, \"{member.Name}_SpaceId\");");
            }

            sb.AppendLine("        }");
            membersCount++;
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine($"    public {overrideAttribute}(Guid, Guid)[] ReadInject(FileStream stream)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var guids = new (Guid, Guid)[{serializableMembers.Count}];");
        sb.AppendLine($"        for (int i = 0; i < {serializableMembers.Count}; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            guids[i] = (Query.Serializer.ReadGuid(stream), Query.Serializer.ReadGuid(stream));");
        sb.AppendLine("        }");
        sb.AppendLine("        return guids;");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine($"    public {overrideAttribute}void Inject((Guid, Guid)[] guids)");
        sb.AppendLine("    {");
        var serializableIndex = 0;
        foreach (var member in injectableMembers)
            if (member.InjectType == InjectType.AutoInject)
            {
                sb.AppendLine(
                    $"        this.{member.Name} ??= Command.AutoInject<{member.TypeFullName}>(Entity.CurrentSpace);");
            }
            else
            {
                if (member.ReferenceKind == ReferenceKind.Logic)
                    sb.AppendLine(
                        $"        this.{member.Name} = Command.Inject<Entity>(guids[{serializableIndex}].Item1, guids[{serializableIndex}].Item2).GetLogic<{member.TypeFullName}>();");
                else if (member.ReferenceKind == ReferenceKind.IData)
                    sb.AppendLine(
                        $"        this.{member.Name} = Command.Inject<Entity>(guids[{serializableIndex}].Item1, guids[{serializableIndex}].Item2).GetData<{member.TypeFullName}>();");
                else
                    sb.AppendLine(
                        $"        this.{member.Name} = Command.Inject<{member.TypeFullName}>(guids[{serializableIndex}].Item1, guids[{serializableIndex}].Item2);");
                serializableIndex++;
            }

        sb.AppendLine("    }");

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static List<InjectableMember> CollectInjectableMembers(
        INamedTypeSymbol symbol,
        INamedTypeSymbol? dataInterface,
        INamedTypeSymbol? logicBaseType,
        INamedTypeSymbol? referenceableInterface)
    {
        var members = new List<InjectableMember>();
        var isLogic = symbol.InheritsFrom(logicBaseType);

        var allDeclared = symbol.GetMembers().Where(m => m is IFieldSymbol or IPropertySymbol);

        foreach (var m in allDeclared)
        {
            var type = m is IFieldSymbol f ? f.Type : ((IPropertySymbol)m).Type;

            if (isLogic)
            {
                var hasInject = m.GetAttributes().Any(a => a.AttributeClass?.Name is "Inject" or "InjectAttribute");
                var hasAutoInject = m.GetAttributes()
                    .Any(a => a.AttributeClass?.Name is "AutoInject" or "AutoInjectAttribute");

                if (!hasInject && !hasAutoInject) continue;

                var injectType = hasAutoInject ? InjectType.AutoInject : InjectType.Inject;
                var refKind = GetReferenceKind(type, dataInterface, logicBaseType, referenceableInterface);

                members.Add(new InjectableMember
                {
                    Name = m.Name, 
                    TypeFullName = type.ToDisplayString(), 
                    InjectType = injectType, 
                    ReferenceKind = refKind
                });
            }
            else
            {
                if (type.ImplementsInterface(referenceableInterface) ||
                    type.ImplementsInterface(dataInterface) ||
                    type.InheritsFrom(logicBaseType))
                {
                    var refKind = GetReferenceKind(type, dataInterface, logicBaseType, referenceableInterface);
                    members.Add(new InjectableMember
                    {
                        Name = m.Name, 
                        TypeFullName = type.ToDisplayString(), 
                        InjectType = InjectType.Inject, 
                        ReferenceKind = refKind
                    });
                }
            }
        }

        return members;
    }

    private static ReferenceKind GetReferenceKind(
        ITypeSymbol type,
        INamedTypeSymbol? dataInterface,
        INamedTypeSymbol? logicBaseType,
        INamedTypeSymbol? referenceableInterface)
    {
        if (type.InheritsFrom(logicBaseType)) return ReferenceKind.Logic;
        if (type.ImplementsInterface(dataInterface)) return ReferenceKind.IData;
        return ReferenceKind.Referenceable;
    }
}