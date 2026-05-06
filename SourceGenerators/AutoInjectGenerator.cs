using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Orbitality.SourceGenerators;

[Generator]
public class AutoInjectGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var fields = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is FieldDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (ctx, _) => (FieldDeclarationSyntax)ctx.Node
            );

        var combined = context.CompilationProvider.Combine(fields.Collect());

        context.RegisterSourceOutput(combined, (spc, source) =>
        {
            var (compilation, fieldsList) = source;

            var grouped = new Dictionary<INamedTypeSymbol, List<IFieldSymbol>>();

            foreach (var fieldDecl in fieldsList)
            {
                var model = compilation.GetSemanticModel(fieldDecl.SyntaxTree);

                foreach (var v in fieldDecl.Declaration.Variables)
                {
                    var fieldSymbol = model.GetDeclaredSymbol(v) as IFieldSymbol;
                    if (fieldSymbol == null) continue;

                    var hasAttr = fieldSymbol.GetAttributes()
                        .Any(a => a.AttributeClass?.Name == "AutoInjectAttribute");

                    if (!hasAttr) continue;

                    var classSymbol = fieldSymbol.ContainingType;

                    if (!grouped.TryGetValue(classSymbol, out var list))
                    {
                        list = new List<IFieldSymbol>();
                        grouped[classSymbol] = list;
                    }

                    list.Add(fieldSymbol);
                }
            }

            foreach (var pair in grouped)
            {
                var cls = pair.Key;
                var fieldsSymbols = pair.Value;
                var ns = cls.ContainingNamespace.ToDisplayString();
                var className = cls.Name;

                var sb = new StringBuilder($@"
namespace {ns}
{{
    public partial class {className}
    {{
        public void __AutoInject(Orbitality.Containers.DependencyPool c)
        {{
");

                foreach (var field in fieldsSymbols)
                {
                    sb.AppendLine($"this.{field.Name} ??= c.Get<{field.Type.ToDisplayString()}>();");
                }

                sb.Append(@"
        }
    }
}");

                spc.AddSource($"{className}_AutoInject.g.cs", sb.ToString());
            }
        });
    }
}