using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Orbitality.SourceGenerators;

[Generator]
public class AutoInjectGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) as INamedTypeSymbol)
            .Where(static m => m is not null && IsLogicSubclass(m));

        context.RegisterSourceOutput(classes, Execute);
    }

    private static bool IsLogicSubclass(INamedTypeSymbol symbol)
    {
        var baseType = symbol.BaseType;
        while (baseType != null)
        {
            if (baseType.ToDisplayString() == "Engine.Logic") return true;
            baseType = baseType.BaseType;
        }
        return false;
    }

    private void Execute(SourceProductionContext context, INamedTypeSymbol classSymbol)
    {
        var className = classSymbol.Name;
        var ns = classSymbol.ContainingNamespace?.ToDisplayString() ?? "Engine";

        var fieldsToInject = classSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => !f.IsStatic && f.GetAttributes()
                .Any(a => a.AttributeClass?.Name == "AutoInjectAttribute"))
            .ToList();

        if (fieldsToInject.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine($$"""
            using System;
            using Microsoft.Extensions.DependencyInjection;

            namespace {{ns}};

            partial class {{className}}
            {
                partial void ResolveDependencies()
                {
            """);

        foreach (var field in fieldsToInject)
        {
            var fieldType = field.Type.ToDisplayString();
            sb.AppendLine($$"""        {{field.Name}} = ServiceProvider.GetRequiredService<{{fieldType}}>();""");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource($"{className}.g.cs", sb.ToString());
    }
}