using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Solas.SourceGenerators;

[Generator]
public class InjectGenerator : IIncrementalGenerator
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
                        .Any(a => a.AttributeClass?.Name is "AutoInjectAttribute" or "InjectAttribute");

                    if (!hasAttr) continue;

                    var symbol = fieldSymbol.ContainingType;

                    if (!grouped.TryGetValue(symbol, out var list))
                    {
                        list = new List<IFieldSymbol>();
                        grouped[symbol] = list;
                    }

                    list.Add(fieldSymbol);
                }
            }

            foreach (var pair in grouped)
            {
                var typeSymbol = pair.Key;
                var fieldsSymbols = pair.Value;
                var ns = typeSymbol.ContainingNamespace.ToDisplayString();
                var className = typeSymbol.Name;
                string keyword =
                    typeSymbol.TypeKind switch
                    {
                        TypeKind.Struct => "struct",
                        _ => "class"
                    };
                
                var sb = new StringBuilder($@"
namespace {ns}
{{
    public partial {keyword} {className}
    {{
        public void Inject((Guid, Guid)[] guids)
        {{
            var sys = Solas.Engine.Context.DISystem;
");
                var count = 0;
                foreach (var field in fieldsSymbols)
                {
                    if (field.GetAttributes().Any(a => a.AttributeClass?.Name is "AutoInjectAttribute"))
                    {
                        sb.AppendLine($"            {field.Name} ??= sys.AutoInject<{field.Type.ToDisplayString()}>(Entity.CurrentSpace);");
                    }
                    else if (field.Type.BaseType?.ToDisplayString() is "Solas.Components.Logic")
                    {
                        sb.AppendLine($"            {field.Name} ??= sys.Inject(guids[{count}].Item1, guids[{count}].Item2).GetLogic<{field.Type.ToDisplayString()}>();");
                    }
                    else if (field.Type.Interfaces.Any(x => x.ToDisplayString() is "Solas.Components.IData"))
                    {
                        sb.AppendLine($"            {field.Name} ??= sys.Inject(guids[{count}].Item1, guids[{count}].Item2).GetData<{field.Type.ToDisplayString()}>();");
                    }
                    else
                    {
                        sb.AppendLine($"            {field.Name} ??= sys.Inject<{field.Type.ToDisplayString()}>(guids[{count}].Item1);");
                    }

                    count++;
                }

                sb.Append(
      @"        }
    }
}");

                spc.AddSource($"{className}.Inject.g.cs", sb.ToString());
            }
        });
    }
}