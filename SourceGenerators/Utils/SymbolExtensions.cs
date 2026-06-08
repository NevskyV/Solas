using Microsoft.CodeAnalysis;

namespace Solas.SourceGenerators.Utils;

public static class SymbolExtensions
{
    public static bool ImplementsInterface(this ITypeSymbol type, INamedTypeSymbol? interfaceSymbol)
    {
        if (interfaceSymbol == null) return false;
        return SymbolEqualityComparer.Default.Equals(type, interfaceSymbol) ||
               type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, interfaceSymbol));
    }

    public static bool InheritsFrom(this ITypeSymbol type, INamedTypeSymbol? baseSymbol)
    {
        if (baseSymbol == null) return false;
        ITypeSymbol? current = type;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseSymbol))
                return true;
            current = current.BaseType;
        }
        return false;
    }

    public static bool IsNullable(this ITypeSymbol type, out ITypeSymbol? inner)
    {
        inner = null;
        if (type is not INamedTypeSymbol named || !named.IsGenericType)
            return false;

        if (named.ConstructedFrom.ToDisplayString() != "System.Nullable<T>")
            return false;

        inner = named.TypeArguments[0];
        return true;
    }

    public static bool IsEnumerable(this ITypeSymbol type, out ITypeSymbol? item)
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

    public static bool IsDataProperty(this ITypeSymbol type, out ITypeSymbol? inner)
    {
        inner = null;
        if (type is not INamedTypeSymbol named || !named.IsGenericType)
            return false;

        if (named.ConstructedFrom.ToDisplayString() != "Solas.ComponentUtils.DataProperty<T>")
            return false;

        inner = named.TypeArguments[0];
        return true;
    }

    public static bool CanBeNull(this ITypeSymbol type)
    {
        if (type.IsNullable(out _))
            return true;

        if (type.IsValueType)
            return false;

        return type.NullableAnnotation != NullableAnnotation.NotAnnotated;
    }

    public static bool CanBeNewed(this ITypeSymbol type)
    {
        if (type is ITypeParameterSymbol typeParam)
            return typeParam.HasConstructorConstraint;

        if (type.IsValueType)
            return true;

        if (type is INamedTypeSymbol namedType)
        {
            if (namedType.IsAbstract)
                return false;

            return namedType.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public);
        }

        return false;
    }

    public static bool IsReferenceField(this ITypeSymbol type, BinarySerializerGenerator.GenerationContext context)
    {
        if (type.IsEntity(context) || type.IsLogic(context) || type.IsData(context))
            return true;

        if (context.ReferenceableInterface == null)
            return false;

        return type.ImplementsInterface(context.ReferenceableInterface) ||
               SymbolEqualityComparer.Default.Equals(type, context.ReferenceableInterface);
    }

    public static bool IsEntity(this ITypeSymbol type, BinarySerializerGenerator.GenerationContext context) =>
        context.EntityType != null && SymbolEqualityComparer.Default.Equals(type, context.EntityType);

    public static bool IsLogic(this ITypeSymbol type, BinarySerializerGenerator.GenerationContext context) =>
        context.LogicType != null && type.InheritsFrom(context.LogicType);

    public static bool IsData(this ITypeSymbol type, BinarySerializerGenerator.GenerationContext context) =>
        context.DataInterface != null && type.ImplementsInterface(context.DataInterface);
}