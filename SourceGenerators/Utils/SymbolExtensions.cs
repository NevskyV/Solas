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
        var current = type;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseSymbol))
                return true;
            current = current.BaseType;
        }

        return false;
    }

    public static bool IsPrimitive(this ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_Byte or
                SpecialType.System_Boolean or
                SpecialType.System_Char or
                SpecialType.System_String or
                SpecialType.System_Int16 or
                SpecialType.System_Int32 or
                SpecialType.System_Int64 or
                SpecialType.System_UInt16 or
                SpecialType.System_UInt32 or
                SpecialType.System_UInt64 or
                SpecialType.System_Single or
                SpecialType.System_Double => true,
            _ => type.ToDisplayString() == "System.Guid"
        };
    }

    public static bool IsDataProperty(this ITypeSymbol type, out ITypeSymbol? inner)
    {
        inner = null;
        if (type is not INamedTypeSymbol named || !named.IsGenericType)
            return false;

        if (!named.ToDisplayString().StartsWith("Solas.ComponentUtils.DataProperty<"))
            return false;

        inner = named.TypeArguments[0];
        return true;
    }
}