using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Solas.SourceGenerators.Utils;

namespace Solas.SourceGenerators;

[Generator]
public sealed class DataSerializationRegistrationGenerator : IIncrementalGenerator
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

            if (dataInterface == null) return;

            var registeredTypes = new List<string>();

            foreach (var syntax in syntaxes)
            {
                var model = compilation.GetSemanticModel(syntax!.SyntaxTree);
                if (model.GetDeclaredSymbol(syntax) is not INamedTypeSymbol symbol) continue;

                if (symbol.ImplementsInterface(dataInterface) && !symbol.IsAbstract &&
                    symbol.TypeKind is TypeKind.Class or TypeKind.Struct) registeredTypes.Add(symbol.ToDisplayString());
            }
            
            if (registeredTypes.Count == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine("using Solas;");
            sb.AppendLine();
            sb.AppendLine("namespace Solas.Generated;");
            sb.AppendLine();
            sb.AppendLine("public class DataSerializationRegistration : Solas.Registries.IDataRegistration");
            sb.AppendLine("{");
            sb.AppendLine("    public void Add(Solas.Registries.Registry registry)");
            sb.AppendLine("    {");
            sb.AppendLine("        var trueRegistry = (Solas.Registries.DataSerializationRegistry) registry;");
            foreach (var type in registeredTypes)
                sb.AppendLine($"        trueRegistry.Register<{type}>(\"{type}, {assemblyName}\");");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            ctx.AddSource("DataRegistration.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        });
    }
}