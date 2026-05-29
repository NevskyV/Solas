using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Solas.SourceGenerators;

[Generator]
public class SettingsFileGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var structDeclaration = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is StructDeclarationSyntax cds && cds.AttributeLists.Count > 0,
                transform: static (ctx, _) => ctx.Node as StructDeclarationSyntax
            )
            .Where(static c => c is not null);

        var compilationAndStructs = context.CompilationProvider.Combine(structDeclaration.Collect());

        context.RegisterSourceOutput(compilationAndStructs, (spc, source) =>
        {
            var (compilation, structs) = source;
            string assemblyName = compilation.AssemblyName ?? "UnknownAssembly";
            foreach (var sds in structs)
            {
                var builder = new StringBuilder();
                var model = compilation.GetSemanticModel(sds.SyntaxTree);
                if (model.GetDeclaredSymbol(sds) is not INamedTypeSymbol symbol) continue;

                var attrs = symbol.GetAttributes();
                var attr = attrs.FirstOrDefault(a => a.AttributeClass?.Name == "SettingsSectionAttribute");
                if (attr == null) return;

                var fullName = symbol.ToDisplayString();
                var className = symbol.Name;
                string ns =
                    symbol.ContainingNamespace.ToDisplayString();
                builder.Append($@"
using Solas.Attributes;
using Solas.Components;
using System.Runtime.CompilerServices;
namespace {ns}
{{
    public partial struct {className} : IData
    {{
        [ModuleInitializer]
        public static void CreateBinary()
        {{
            var dir = AppDomain.CurrentDomain.BaseDirectory + @""\Solas\Settings\"";
            var path = dir + ""{className}.set"";
            
            if (!File.Exists(path))
            {{
                using var stream = File.Open(path, FileMode.Create, FileAccess.Write);
                using var writer = new BinaryWriter(stream);
                writer.Write(""{fullName}, {assemblyName}"");
                Write(writer, new {fullName}());
            }}
        }}
    }}
}}");
                spc.AddSource($"{className}FileGenerator.g.cs", builder.ToString());
            }
        });
    }
}