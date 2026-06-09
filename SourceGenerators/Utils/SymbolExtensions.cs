using Microsoft.CodeAnalysis;

namespace Solas.SourceGenerators.Utils;

public static class SymbolExtensions
{
    extension(ITypeSymbol type)
    {
        public bool ImplementsInterface(INamedTypeSymbol? interfaceSymbol)
        {
            if (interfaceSymbol == null) return false;
            return SymbolEqualityComparer.Default.Equals(type, interfaceSymbol) ||
                   type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, interfaceSymbol));
        }

        private bool InheritsFrom(INamedTypeSymbol? baseSymbol)
        {
            if (baseSymbol == null) return false;
            var current = type;
            while (current != null)
            {
                if (SymbolEqualityComparer.Default.Equals(current, baseSymbol))
                    return true;
                current = current.BaseType;
            }

            return false;
        }

        public bool IsNullable(out ITypeSymbol? inner)
        {
            inner = null;
            if (type is not INamedTypeSymbol { IsGenericType: true } named)
                return false;

            if (named.ConstructedFrom.ToDisplayString() != "System.Nullable<T>")
                return false;

            inner = named.TypeArguments[0];
            return true;
        }

        public bool IsEnumerable(out ITypeSymbol? item)
        {
            item = null;
            if (type is IArrayTypeSymbol)
                return false;

            var enumerable = type.AllInterfaces.FirstOrDefault(i =>
                i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");

            if (enumerable == null)
                return false;

            item = enumerable.TypeArguments[0];
            return true;
        }

        public bool IsDataProperty(out ITypeSymbol? inner)
        {
            inner = null;
            if (type is not INamedTypeSymbol { IsGenericType: true } named)
                return false;

            if (named.ConstructedFrom.ToDisplayString() != "Solas.ComponentUtils.DataProperty<T>")
                return false;

            inner = named.TypeArguments[0];
            return true;
        }

        public bool CanBeNull()
        {
            if (type.IsNullable(out _))
                return true;

            if (type.IsValueType)
                return false;

            return type.NullableAnnotation != NullableAnnotation.NotAnnotated;
        }

        public bool CanBeNewed()
        {
            if (type is ITypeParameterSymbol typeParam)
                return typeParam.HasConstructorConstraint;

            if (type.IsValueType)
                return true;

            if (type is INamedTypeSymbol namedType)
            {
                if (namedType.IsAbstract)
                    return false;

                return namedType.InstanceConstructors.Any(c =>
                    c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public);
            }

            return false;
        }

        public bool IsReferenceField(BinarySerializerGenerator.GenerationContext context)
        {
            if (type.IsEntity(context) || type.IsLogic(context) || type.IsData(context))
                return true;

            if (context.ReferenceableInterface == null)
                return false;

            return type.ImplementsInterface(context.ReferenceableInterface) ||
                   SymbolEqualityComparer.Default.Equals(type, context.ReferenceableInterface);
        }

        public bool IsEntity(BinarySerializerGenerator.GenerationContext context)
        {
            return context.EntityType != null && SymbolEqualityComparer.Default.Equals(type, context.EntityType);
        }

        public bool IsLogic(BinarySerializerGenerator.GenerationContext context)
        {
            return context.LogicType != null && type.InheritsFrom(context.LogicType);
        }

        public bool IsData(BinarySerializerGenerator.GenerationContext context)
        {
            return context.DataInterface != null && type.ImplementsInterface(context.DataInterface);
        }
    }
}